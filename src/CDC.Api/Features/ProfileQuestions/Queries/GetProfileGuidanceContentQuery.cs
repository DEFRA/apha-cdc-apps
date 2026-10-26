using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileQuestions.Queries;

/// <summary>Retrieves what the four user guidance reports print.</summary>
public sealed record GetProfileGuidanceContentQuery : IRequest<Result<ProfileGuidanceContentDto>>;

/// <summary>Handles <see cref="GetProfileGuidanceContentQuery"/>.</summary>
/// <param name="profileQuestionService">Profile question application service.</param>
/// <param name="userContext">The acting user, used to enforce the legacy report permission.</param>
/// <param name="logger">Structured logger.</param>
public sealed class GetProfileGuidanceContentQueryHandler(
    IProfileQuestionService profileQuestionService,
    IUserContext userContext,
    ILogger<GetProfileGuidanceContentQueryHandler> logger)
    : IRequestHandler<GetProfileGuidanceContentQuery, Result<ProfileGuidanceContentDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The content, or a forbidden result when the user cannot edit user guidance.</returns>
    public async Task<Result<ProfileGuidanceContentDto>> Handle(GetProfileGuidanceContentQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Legacy GetProfileGuidanceReportCommand.CanGetReport(): profile editors only, never a user management system.
        if (!userContext.IsProfileEditor || userContext.IsUserManagementSystem)
        {
            logger.GuidanceContentRefused();

            return Result.Forbidden<ProfileGuidanceContentDto>("You do not have permission to get the user guidance reports.");
        }

        return Result.Success(await profileQuestionService.GetProfileGuidanceContentAsync(cancellationToken));
    }
}
