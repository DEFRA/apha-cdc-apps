using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Interfaces;
using MediatR;

namespace CDC.Api.Features.UserAdmin.Commands;

/// <summary>Handles <see cref="UpdateReviewEmailSubscriptionCommand"/>.</summary>
/// <param name="userAdminService">User administration application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateReviewEmailSubscriptionCommandHandler(
    IUserAdminService userAdminService,
    ILogger<UpdateReviewEmailSubscriptionCommandHandler> logger)
    : IRequestHandler<UpdateReviewEmailSubscriptionCommand, Result<UpdateReviewEmailSubscriptionResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The subscription change to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The stored subscription state, or a not-found/conflict result.</returns>
    public async Task<Result<UpdateReviewEmailSubscriptionResultDto>> Handle(
        UpdateReviewEmailSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var result = await userAdminService.UpdateReviewEmailSubscriptionAsync(request, cancellationToken);

            return result is null
                ? Result.NotFound<UpdateReviewEmailSubscriptionResultDto>($"No user exists with id '{request.UserId}'.")
                : Result.Success(result);
        }
        catch (ConcurrencyException exception)
        {
            logger.SubscriptionConcurrencyConflict(request.UserId);

            return Result.Conflict<UpdateReviewEmailSubscriptionResultDto>(exception.Message);
        }
    }
}
