using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.Species.Queries;

/// <summary>
/// Retrieves the species selected against a named disease filter.
/// </summary>
/// <param name="DiseaseName">The disease name to filter by.</param>
public sealed record GetAllSelectedSpeciesQuery(string DiseaseName) : IRequest<Result<IReadOnlyList<SelectedSpeciesDto>>>;

/// <summary>
/// Validates <see cref="GetAllSelectedSpeciesQuery"/>.
/// </summary>
public sealed class GetAllSelectedSpeciesQueryValidator : AbstractValidator<GetAllSelectedSpeciesQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetAllSelectedSpeciesQueryValidator"/> class.</summary>
    public GetAllSelectedSpeciesQueryValidator()
    {
        // spgaSelectedDiseaseSpecies declares @DiseaseName as nvarchar(500).
        RuleFor(query => query.DiseaseName)
            .NotEmpty().WithMessage("A disease name is required.")
            .MaximumLength(500).WithMessage("A disease name must be 500 characters or fewer.");
    }
}

/// <summary>
/// Handles <see cref="GetAllSelectedSpeciesQuery"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
public sealed class GetAllSelectedSpeciesQueryHandler(ISpeciesService speciesService)
    : IRequestHandler<GetAllSelectedSpeciesQuery, Result<IReadOnlyList<SelectedSpeciesDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching species; empty when the disease name is unknown.</returns>
    public async Task<Result<IReadOnlyList<SelectedSpeciesDto>>> Handle(GetAllSelectedSpeciesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var species = await speciesService.GetAllSelectedSpeciesAsync(request.DiseaseName, cancellationToken);

        return Result.Success(species);
    }
}
