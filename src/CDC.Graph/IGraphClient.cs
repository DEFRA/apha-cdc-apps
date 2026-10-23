namespace CDC.Graph;

/// <summary>A person found in the directory.</summary>
/// <param name="Id">The Entra ID object ID.</param>
/// <param name="DisplayName">The person's full name.</param>
/// <param name="Organisation">The company name, or the department when none is set; <see langword="null"/> when neither is.</param>
public sealed record GraphUser(string Id, string DisplayName, string? Organisation);

/// <summary>Reads directory data from Microsoft Graph using the app's own identity.</summary>
public interface IGraphClient
{
    /// <summary>Finds a user by their on-premises logon name (the part of <c>DOMAIN\name</c> after the backslash).</summary>
    /// <param name="samAccountName">The logon name, without the domain.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user, or <see langword="null"/> when none or more than one matches (an ambiguous match is never guessed).</returns>
    /// <exception cref="GraphException">The token request or the Graph call failed.</exception>
    Task<GraphUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken);
}
