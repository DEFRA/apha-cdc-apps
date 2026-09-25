using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>
/// Retrieves the name and active state of one species, by species identifier. Mirrors the
/// legacy <c>ProfileManagementService.GetAffectedSpecies</c> (<c>spgSpeciesNameById</c>), which
/// looks a species up directly rather than by profile.
/// </summary>
/// <param name="SpeciesId">The species to read.</param>
public sealed record GetAffectedSpeciesQuery(Guid SpeciesId) : IRequest<Result<AffectedSpeciesDto>>;

/// <summary>Validates <see cref="GetAffectedSpeciesQuery"/>.</summary>
public sealed class GetAffectedSpeciesQueryValidator : AbstractValidator<GetAffectedSpeciesQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetAffectedSpeciesQueryValidator"/> class.</summary>
    public GetAffectedSpeciesQueryValidator()
    {
        RuleFor(query => query.SpeciesId)
            .NotEmpty().WithMessage("A species id is required.");
    }
}

/// <summary>Handles <see cref="GetAffectedSpeciesQuery"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class GetAffectedSpeciesQueryHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<GetAffectedSpeciesQuery, Result<AffectedSpeciesDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The species' name and active state, or a not-found result.</returns>
    public async Task<Result<AffectedSpeciesDto>> Handle(GetAffectedSpeciesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var species = await profileManagementService.GetAffectedSpeciesAsync(request.SpeciesId, cancellationToken);

        return species is null
            ? Result.NotFound<AffectedSpeciesDto>($"Species '{request.SpeciesId}' was not found.")
            : Result.Success(species);
    }
}
