using CDC.Api.Application;
using CDC.Api.Features.Users.Interfaces;
using CDC.Common.Auth;
using Serilog.Context;

namespace CDC.Api.Middleware;

/// <summary>
/// Resolves the signed-in user a request is made for. CDC.Web forwards their <c>[User].Id</c> in the
/// <see cref="ActingUserHeader"/> header and this loads that row's role flags into
/// <see cref="RequestUserContext"/>, as the legacy app read them from the signed-in
/// <c>ProfilesIdentity</c>. A missing, malformed or unknown id leaves the request anonymous.
/// </summary>
/// <remarks>
/// The header is not authenticated: CDC.Api is reachable only from inside the VPC, where CDC.Web is
/// its only caller. The header names the user but never carries a role - the flags always come from
/// the database.
/// </remarks>
/// <param name="next">The next component in the pipeline.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ActingUserMiddleware(RequestDelegate next, ILogger<ActingUserMiddleware> logger)
{
    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current request.</param>
    /// <param name="userContext">The request's user context, filled in here.</param>
    /// <param name="users">User data access, used to look the acting user up.</param>
    /// <returns>A task that completes when the rest of the pipeline has run.</returns>
    public async Task InvokeAsync(HttpContext context, RequestUserContext userContext, IUserRepository users)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(users);

        if (TryReadUserId(context, out var userId))
        {
            var user = await users.GetActingUserAsync(userId, context.RequestAborted);

            if (user is null)
            {
                logger.UnknownActingUser(userId);
            }
            else
            {
                userContext.SetActingUser(user);
            }
        }

        // The id is an opaque GUID, so it is safe to put on every log line for the request.
        using var logScope = userContext.UserId is { } actingUserId
            ? LogContext.PushProperty("ActingUserId", actingUserId)
            : null;

        await next(context);
    }

    private static bool TryReadUserId(HttpContext context, out Guid userId)
    {
        userId = Guid.Empty;

        // More than one value is ambiguous, so it names nobody.
        return context.Request.Headers.TryGetValue(ActingUserHeader.Name, out var values)
            && values.Count == 1
            && Guid.TryParseExact(values[0], "D", out userId)
            && userId != Guid.Empty;
    }
}

/// <summary>Source-generated log messages for <see cref="ActingUserMiddleware"/>.</summary>
internal static partial class ActingUserLog
{
    [LoggerMessage(EventId = 1103, Level = LogLevel.Warning, Message = "The acting user {UserId} sent by the caller does not exist; the request is treated as anonymous")]
    public static partial void UnknownActingUser(this ILogger logger, Guid userId);
}

/// <summary>
/// Adds <see cref="ActingUserMiddleware"/> to the request pipeline.
/// </summary>
public static class ActingUserMiddlewareExtensions
{
    /// <summary>
    /// Registers the acting-user middleware. Call this before <c>UseSerilogRequestLogging()</c> so the
    /// request summary line carries the user id.
    /// </summary>
    /// <param name="app">The application pipeline builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseActingUser(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<ActingUserMiddleware>();
    }
}
