using CDC.Api.Domain.Common;
using CDC.Api.Features.PrioritisationVariables.Dtos;
using CDC.Api.Features.PrioritisationVariables.Interfaces;
using MediatR;

namespace CDC.Api.Features.PrioritisationVariables.Queries;

/// <summary>
/// Retrieves every prioritisation category with its criteria.
/// </summary>
public sealed record GetPrioritisationCategoriesQuery : IRequest<Result<IReadOnlyList<PrioritisationCategoryDto>>>;

/// <summary>
/// Handles <see cref="GetPrioritisationCategoriesQuery"/>.
/// </summary>
/// <param name="repository">Prioritisation variables data access.</param>
public sealed class GetPrioritisationCategoriesQueryHandler(IPrioritisationVariablesRepository repository)
    : IRequestHandler<GetPrioritisationCategoriesQuery, Result<IReadOnlyList<PrioritisationCategoryDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every category, each with its criteria.</returns>
    public async Task<Result<IReadOnlyList<PrioritisationCategoryDto>>> Handle(
        GetPrioritisationCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await repository.GetCategoriesWithCriteriaAsync(cancellationToken);

        IReadOnlyList<PrioritisationCategoryDto> dtos = [.. categories.Select(category => new PrioritisationCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Criteria = [.. category.Criteria.Select(criterion => new PrioritisationCriterionDto
            {
                Id = criterion.Id,
                Code = criterion.Code,
                Name = criterion.Name,
                Weight = criterion.Weight,
                Values = [.. criterion.Values.Select(value => new PrioritisationCriterionValueDto
                {
                    Id = value.Id,
                    Value = value.Value,
                    Score = value.Score
                })]
            })]
        })];

        return Result.Success(dtos);
    }
}
