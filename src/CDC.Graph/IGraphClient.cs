namespace CDC.Graph;

/// <summary>A person found in the directory.</summary>
/// <param name="Id">The Entra ID object ID.</param>
/// <param name="SamAccountName">The on-premises logon name.</param>
/// <param name="Mail">The email address.</param>
/// <param name="GivenName">The first name.</param>
/// <param name="Surname">The last name.</param>
public sealed record GraphUser(string Id, string? SamAccountName, string? Mail, string? GivenName, string? Surname)
{
    /// <summary>Gets the given name and surname joined, or an empty string when neither is set.</summary>
    public string DisplayName => $"{GivenName?.Trim()} {Surname?.Trim()}".Trim();
}

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
