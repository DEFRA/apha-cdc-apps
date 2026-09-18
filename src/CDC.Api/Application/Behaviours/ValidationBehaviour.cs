using FluentValidation;
using MediatR;

namespace CDC.Api.Application.Behaviours;

/// <summary>
/// Runs every registered FluentValidation validator for a request before its handler executes.
/// Failures are thrown as <see cref="ValidationException"/> and converted to an RFC 7807
/// response by <c>GlobalExceptionMiddleware</c>.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The MediatR response type.</typeparam>
/// <param name="validators">Validators registered for <typeparamref name="TRequest"/>.</param>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Validates the request, then invokes the next step in the pipeline.</summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="next">The next step in the pipeline.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The handler's response.</returns>
    /// <exception cref="ValidationException">Thrown when any validator reports a failure.</exception>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var applicable = validators.ToArray();

        if (applicable.Length == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            applicable.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .Where(result => !result.IsValid)
            .SelectMany(result => result.Errors)
            .ToArray();

        return failures.Length > 0
            ? throw new ValidationException(failures)
            : await next(cancellationToken);
    }
}
