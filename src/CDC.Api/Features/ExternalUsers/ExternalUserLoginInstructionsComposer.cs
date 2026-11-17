using CDC.Api.Domain.Entities;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Interfaces;

namespace CDC.Api.Features.ExternalUsers;

/// <summary>
/// Builds the login instructions email for an external user. The subject and text are written in the GOV.UK Notify
/// template; the values it takes are <c>((name))</c>, the user's name, and <c>((AppUrl))</c>, the address of the external
/// sign-in page (<c>App:BaseUrl</c> plus <c>Account/Login</c>), so the link goes straight to the CIDM sign-in.
/// </summary>
public static class ExternalUserLoginInstructionsComposer
{
    /// <summary>The Notify template for the external user login instructions email. The same in every environment.</summary>
    public const string TemplateId = "8c79693e-ad6e-4bc1-9b86-0b656b55b7de";

    /// <summary>The template placeholder for the address of the external sign-in page.</summary>
    public const string AppUrlField = "AppUrl";

    /// <summary>The template placeholder for the user's name.</summary>
    public const string NameField = "name";

    /// <summary>The path of the external (CIDM) sign-in page, relative to the application's base address.</summary>
    public const string LoginPath = "Account/Login";

    /// <summary>Composes the email for one user.</summary>
    /// <param name="user">The user to email.</param>
    /// <param name="homePageUrl">The address D2R2 is opened at (<c>App:BaseUrl</c>).</param>
    /// <returns>The message, or <see langword="null"/> when <paramref name="homePageUrl"/> is not a valid web address.</returns>
    public static EmailMessage? Compose(ExternalUserLoginRecipient user, string homePageUrl)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!NotifyMarkdown.TryGetWebUri(homePageUrl, out var homePage))
        {
            return null;
        }

        // Built from the path alone so a base address with or without a trailing slash (or an application path) both work,
        // and any query string or fragment on the configured address is not carried into the link.
        var loginUrl = new Uri($"{homePage.GetLeftPart(UriPartial.Path).TrimEnd('/')}/{LoginPath}");

        var emailAddress = user.EmailAddress.Trim();

        // The name is typed in by an administrator and Notify renders markdown, so markup is stripped; Notify also rejects an
        // empty value, so a user with no usable name is greeted by their email address instead.
        var name = NotifyMarkdown.Clean(user.FullName ?? string.Empty).Trim();

        return EmailMessage.FromTemplate(
            emailAddress,
            TemplateId,
            new Dictionary<string, string>
            {
                [NameField] = name.Length > 0 ? name : emailAddress,
                [AppUrlField] = loginUrl.AbsoluteUri
            });
    }
}
