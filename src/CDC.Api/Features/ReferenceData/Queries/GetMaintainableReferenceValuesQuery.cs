using CDC.Api.Domain.Common;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using MediatR;

namespace CDC.Api.Features.ReferenceData.Queries;

/// <summary>Retrieves every value of one maintainable reference table.</summary>
/// <param name="ReferenceTableId">The table to read.</param>
public sealed record GetMaintainableReferenceValuesQuery(Guid ReferenceTableId)
    : IRequest<Result<IReadOnlyList<MaintainableReferenceValueDto>>>;

/// <summary>Handles <see cref="GetMaintainableReferenceValuesQuery"/>.</summary>
/// <param name="repository">Reference data maintenance access.</param>
public sealed class GetMaintainableReferenceValuesQueryHandler(IReferenceDataMaintenanceRepository repository)
    : IRequestHandler<GetMaintainableReferenceValuesQuery, Result<IReadOnlyList<MaintainableReferenceValueDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The values in sequence order, or not found when the table is not maintainable.</returns>
    public async Task<Result<IReadOnlyList<MaintainableReferenceValueDto>>> Handle(
        GetMaintainableReferenceValuesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var values = await repository.GetMaintainableValuesAsync(request.ReferenceTableId, cancellationToken);

        return values is null
            ? Result.NotFound<IReadOnlyList<MaintainableReferenceValueDto>>("The reference table does not exist or is not maintainable.")
            : Result.Success(values);
    }
}
