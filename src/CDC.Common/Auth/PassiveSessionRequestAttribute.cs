namespace CDC.Common.Auth;

/// <summary>
/// Marks an endpoint that only reports on the signed-in session, such as the session status check behind the
/// "Extend session?" warning. A request to it neither extends the session nor exchanges tokens (see
/// <see cref="TokenRefreshCookieEvents"/>), so checking how long a session has left can never keep an idle session alive.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PassiveSessionRequestAttribute : Attribute;
