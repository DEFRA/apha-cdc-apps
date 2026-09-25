using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>
/// Retrieves default values for a new profile or "what-if" scenario, sourced from the profile
/// version being cloned.
/// </summary>
/// <param name="CloneProfileVersionId">The profile version to read defaults from.</param>
/// <param name="IsWhatIfScenario">Whether the new profile will be a "what-if" scenario.</param>
public sealed record GetNewProfileDefaultsQuery(Guid CloneProfileVersionId, bool IsWhatIfScenario)
    : IRequest<Result<NewProfileDefaultsDto>>;

/// <summary>Validates <see cref="GetNewProfileDefaultsQuery"/>.</summary>
public sealed class GetNewProfileDefaultsQueryValidator : AbstractValidator<GetNewProfileDefaultsQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetNewProfileDefaultsQueryValidator"/> class.</summary>
    public GetNewProfileDefaultsQueryValidator()
    {
        RuleFor(query => query.CloneProfileVersionId)
            .NotEmpty().WithMessage("A profile version id to clone from is required.");
    }
}

/// <summary>Handles <see cref="GetNewProfileDefaultsQuery"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class GetNewProfileDefaultsQueryHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<GetNewProfileDefaultsQuery, Result<NewProfileDefaultsDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The default values, or a not-found result when the source version does not exist.</returns>
    public async Task<Result<NewProfileDefaultsDto>> Handle(GetNewProfileDefaultsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var defaults = await profileManagementService.GetNewProfileDefaultsAsync(
            request.CloneProfileVersionId,
            request.IsWhatIfScenario,
            cancellationToken);

        return defaults is null
            ? Result.NotFound<NewProfileDefaultsDto>($"Profile version '{request.CloneProfileVersionId}' was not found.")
            : Result.Success(defaults);
    }
}
