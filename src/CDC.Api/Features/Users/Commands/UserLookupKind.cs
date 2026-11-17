namespace CDC.Api.Features.Users.Commands;

/// <summary>The kind of sign-in a user is being looked up for.</summary>
public enum UserLookupKind
{
    /// <summary>An internal sign-in, which the identity provider would do through Entra ID.</summary>
    Internal,

    /// <summary>An external sign-in, which the identity provider would do through CIDM.</summary>
    External
}
