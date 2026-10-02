using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Queries;

/// <summary>
/// Retrieves one profile version's recorded answers for one reference section.
/// </summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
/// <param name="ProfileSectionId">The section to read.</param>
public sealed record GetProfileSectionAnswersQuery(Guid ProfileVersionId, Guid ProfileSectionId)
    : IRequest<Result<ProfileSectionAnswersDto>>;

/// <summary>
/// Handles <see cref="GetProfileSectionAnswersQuery"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class GetProfileSectionAnswersQueryHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<GetProfileSectionAnswersQuery, Result<ProfileSectionAnswersDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The section's question names and recorded field values.</returns>
    public async Task<Result<ProfileSectionAnswersDto>> Handle(
        GetProfileSectionAnswersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var answers = await profileSectionService.GetProfileSectionAnswersAsync(
            request.ProfileVersionId,
            request.ProfileSectionId,
            cancellationToken);

        return Result.Success(answers);
    }
}
