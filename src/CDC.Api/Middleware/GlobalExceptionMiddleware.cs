using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CDC.Api.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Middleware;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 <c>application/problem+json</c> responses.
/// Exception messages are only echoed to the client in Development; every other environment
/// gets a generic description plus the trace identifier, so nothing about the database or the
/// internals leaks.
/// </summary>
/// <param name="next">The next component in the pipeline.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="environment">Hosting environment, used to decide how much detail to return.</param>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    /// <summary>Invokes the middleware.</summary>
    /// <param name="context">The current request.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // The status code and headers are already on the wire; all that is left is to make
            // sure the failure is recorded.
            logger.ResponseAlreadyStarted(exception, context.Request.Path);
            return;
        }

        var problem = CreateProblemDetails(context, exception);

        LogException(context, exception, problem.Status ?? StatusCodes.Status500InternalServerError);

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        // Serialised against the runtime type so that ValidationProblemDetails keeps its
        // "errors" member; the declared type alone would drop it.
        var payload = JsonSerializer.Serialize(problem, problem.GetType(), SerializerOptions);

        await context.Response.WriteAsync(payload, context.RequestAborted);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var problem = exception switch
        {
            ValidationException validationException => BuildValidationProblem(validationException),
            NotFoundException => Build(StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ConcurrencyException => Build(StatusCodes.Status409Conflict, "Conflicting change", exception.Message),
            UnauthorizedAccessException => Build(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You do not have permission to perform this action."),
            DbException => Build(
                StatusCodes.Status500InternalServerError,
                "Database error",
                Describe(exception, "A database error prevented the request from completing.")),
            OperationCanceledException => Build(
                StatusCodes.Status499ClientClosedRequest,
                "Client closed request",
                "The request was cancelled before it completed."),
            _ => Build(
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                Describe(exception, "An unexpected error prevented the request from completing."))
        };

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        return problem;
    }

    private static ValidationProblemDetails BuildValidationProblem(ValidationException exception)
    {
        var problem = new ValidationProblemDetails(
            exception.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred",
            Type = ProblemTypes.BadRequest
        };

        return problem;
    }

    private static ProblemDetails Build(int statusCode, string title, string detail) => new()
    {
        Status = statusCode,
        Title = title,
        Detail = detail,
        Type = ProblemTypes.For(statusCode)
    };

    private string Describe(Exception exception, string fallback) =>
        environment.IsDevelopment() ? exception.Message : fallback;

    private void LogException(HttpContext context, Exception exception, int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.UnhandledRequestFailure(exception, context.Request.Method, context.Request.Path, statusCode);
        }
        else
        {
            logger.HandledRequestFailure(exception, context.Request.Method, context.Request.Path, statusCode);
        }
    }
}

/// <summary>
/// RFC 7807 <c>type</c> URIs. These point at the HTTP status code definitions rather than a
/// bespoke registry, which is the recommended default.
/// </summary>
internal static class ProblemTypes
{
    public const string BadRequest = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public static string For(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };
}

/// <summary>Source-generated log messages for <see cref="GlobalExceptionMiddleware"/>.</summary>
internal static partial class GlobalExceptionLog
{
    [LoggerMessage(EventId = 1100, Level = LogLevel.Error, Message = "{Method} {Path} failed with status {StatusCode}")]
    public static partial void UnhandledRequestFailure(this ILogger logger, Exception exception, string method, string path, int statusCode);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "{Method} {Path} rejected with status {StatusCode}")]
    public static partial void HandledRequestFailure(this ILogger logger, Exception exception, string method, string path, int statusCode);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Error, Message = "{Path} failed after the response had started; no problem details could be written")]
    public static partial void ResponseAlreadyStarted(this ILogger logger, Exception exception, string path);
}

/// <summary>
/// Adds <see cref="GlobalExceptionMiddleware"/> to the request pipeline.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    /// <summary>Registers the global exception handler. Call this first, before any other middleware.</summary>
    /// <param name="app">The application pipeline builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
