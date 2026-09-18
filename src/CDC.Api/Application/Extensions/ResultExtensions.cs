using CDC.Api.Domain.Common;
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

        return result.Status switch
        {
            ResultStatus.Success => controller.Ok(result.Value),
            ResultStatus.NotFound => controller.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource not found"),
            ResultStatus.Conflict => controller.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflicting change"),
            _ => controller.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unexpected result")
        };
    }
}
