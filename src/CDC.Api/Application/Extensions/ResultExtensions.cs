using CDC.Api.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Application.Extensions;

/// <summary>
/// Translates a <see cref="Result{T}"/> into an HTTP response.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Returns 200 with the value on success, or an RFC 7807 problem response describing the
    /// failure.
    /// </summary>
    /// <typeparam name="T">The result value type.</typeparam>
    /// <param name="result">The result to translate.</param>
    /// <param name="controller">The controller producing the response.</param>
    /// <returns>The HTTP response.</returns>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return ToProblem(result.Status, result.Error, controller);
    }

    /// <summary>
    /// Returns 204 on success, or an RFC 7807 problem response describing the failure. For
    /// commands that produce no value, represented by MediatR's <see cref="Unit"/>.
    /// </summary>
    /// <param name="result">The result to translate.</param>
    /// <param name="controller">The controller producing the response.</param>
    /// <returns>The HTTP response.</returns>
    public static ActionResult ToNoContentActionResult(this Result<Unit> result, ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return ToProblem(result.Status, result.Error, controller);
    }

    private static ObjectResult ToProblem(ResultStatus status, string? error, ControllerBase controller) => status switch
    {
        ResultStatus.NotFound => controller.Problem(
            detail: error,
            statusCode: StatusCodes.Status404NotFound,
            title: "Resource not found"),
        ResultStatus.Conflict => controller.Problem(
            detail: error,
            statusCode: StatusCodes.Status409Conflict,
            title: "Conflicting change"),
        ResultStatus.Forbidden => controller.Problem(
            detail: error,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Not permitted"),
        ResultStatus.ValidationFailed => controller.Problem(
            detail: error,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation failed"),
        ResultStatus.DependencyFailed => controller.Problem(
            detail: error,
            statusCode: StatusCodes.Status502BadGateway,
            title: "A dependent service failed"),
        _ => controller.Problem(
            detail: error,
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Unexpected result")
    };
}
