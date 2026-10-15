using System.Text.RegularExpressions;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>Validates the title of a bespoke report template, as CDC.Api does and as the legacy page's text filter did.</summary>
public static partial class BespokeTemplateTitle
{
    /// <summary>The widest a title can be.</summary>
    public const int MaxLength = 255;

    /// <summary>The message for a missing title, as the legacy validator worded it.</summary>
    public const string RequiredMessage = "Template title is a required field.";

    /// <summary>The message for a title that is too long.</summary>
    public const string TooLongMessage = "Template title must be 255 characters or fewer.";

    /// <summary>The message for a title with characters the legacy text box did not let a user type.</summary>
    public const string CharactersMessage = "Template title can only contain letters, numbers and spaces.";

    /// <summary>The message for a title another template already has, as the legacy validator worded it.</summary>
    public const string ExistsMessage = "Template with that title already exists.";

    /// <summary>Checks a title.</summary>
    /// <param name="title">The title typed by the user.</param>
    /// <returns>Why the title is not acceptable, or <see langword="null"/> when it is.</returns>
    public static string? Validate(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return RequiredMessage;
        }

        var trimmed = title.Trim();

        if (trimmed.Length > MaxLength)
        {
            return TooLongMessage;
        }

        return AllowedCharacters().IsMatch(trimmed) ? null : CharactersMessage;
    }

    [GeneratedRegex("^[A-Za-z0-9 ]+$")]
    private static partial Regex AllowedCharacters();
}
