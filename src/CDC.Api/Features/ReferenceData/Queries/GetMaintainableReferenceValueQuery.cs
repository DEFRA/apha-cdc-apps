using CDC.Api.Domain.Common;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using MediatR;

namespace CDC.Api.Features.ReferenceData.Queries;

/// <summary>Retrieves one value of a maintainable reference table, with the row version needed to change it.</summary>
/// <param name="ReferenceTableId">The table the value must belong to.</param>
/// <param name="ValueId">The value to read.</param>
public sealed record GetMaintainableReferenceValueQuery(Guid ReferenceTableId, Guid ValueId)
    : IRequest<Result<MaintainableReferenceValueDto>>;

/// <summary>Handles <see cref="GetMaintainableReferenceValueQuery"/>.</summary>
/// <param name="repository">Reference data maintenance access.</param>
public sealed class GetMaintainableReferenceValueQueryHandler(IReferenceDataMaintenanceRepository repository)
    : IRequestHandler<GetMaintainableReferenceValueQuery, Result<MaintainableReferenceValueDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The value, or not found when it is not in that maintainable table.</returns>
    public async Task<Result<MaintainableReferenceValueDto>> Handle(
        GetMaintainableReferenceValueQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // spgReferenceValue does not say which table a value is in, so the table's list is read to check it.
        var values = await repository.GetMaintainableValuesAsync(request.ReferenceTableId, cancellationToken);
        var value = values?.FirstOrDefault(candidate => candidate.Id == request.ValueId);
        var lastUpdated = value is null ? null : await repository.GetValueRowVersionAsync(value.Id, cancellationToken);

        return value is null || lastUpdated is null
            ? Result.NotFound<MaintainableReferenceValueDto>("The reference value does not exist in that maintainable table.")
            : Result.Success(value with { LastUpdated = lastUpdated });
    }
}
