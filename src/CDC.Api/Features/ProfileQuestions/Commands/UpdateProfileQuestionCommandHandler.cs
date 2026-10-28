using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileQuestions.Commands;

/// <summary>Handles <see cref="UpdateProfileQuestionCommand"/>.</summary>
/// <param name="profileQuestionService">Profile question application service.</param>
/// <param name="userContext">The acting user, used to enforce the legacy edit permission.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateProfileQuestionCommandHandler(
    IProfileQuestionService profileQuestionService,
    IUserContext userContext,
    ILogger<UpdateProfileQuestionCommandHandler> logger)
    : IRequestHandler<UpdateProfileQuestionCommand, Result<ProfileQuestionDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated question, a forbidden result, a not-found result, or a conflict result on a stale update.</returns>
    public async Task<Result<ProfileQuestionDto>> Handle(UpdateProfileQuestionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Legacy ProfileQuestion.CanEditObject(): profile editors only, never a user management system.
        if (!userContext.IsProfileEditor || userContext.IsUserManagementSystem)
        {
            logger.UpdateRefused(request.Id);

            return Result.Forbidden<ProfileQuestionDto>("You do not have permission to edit user guidance.");
        }

        try
        {
            var question = await profileQuestionService.UpdateProfileQuestionAsync(request, cancellationToken);

            return question is null
                ? Result.NotFound<ProfileQuestionDto>($"Profile question '{request.Id}' was not found.")
                : Result.Success(question);
        }
        catch (ConcurrencyException exception)
        {
            logger.ConcurrencyConflict(request.Id);

            return Result.Conflict<ProfileQuestionDto>(exception.Message);
        }
    }
}
