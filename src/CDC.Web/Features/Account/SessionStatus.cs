namespace CDC.Web.Features.Account;

/// <summary>What <c>session-timeout.js</c> is told about the signed-in session.</summary>
/// <param name="Authenticated">Whether a signed-in session still exists; false once it has expired.</param>
/// <param name="RemainingSeconds">How long the session has left before it expires, in whole seconds.</param>
/// <param name="AntiforgeryToken">The token to send with a request to extend the session, when the answer is a status check.</param>
/// <param name="AntiforgeryHeaderName">The name of the request header to send <paramref name="AntiforgeryToken"/> in.</param>
public sealed record SessionStatus(bool Authenticated, int RemainingSeconds, string? AntiforgeryToken = null, string? AntiforgeryHeaderName = null)
{
    /// <summary>The answer when there is no signed-in session to report on.</summary>
    public static SessionStatus Ended { get; } = new(false, 0);
}
