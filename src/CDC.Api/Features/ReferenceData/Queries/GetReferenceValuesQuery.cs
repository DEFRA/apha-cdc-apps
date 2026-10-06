using CDC.Api.Domain.Common;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using MediatR;

namespace CDC.Api.Features.ReferenceData.Queries;

/// <summary>Retrieves every value in one reference table.</summary>
/// <param name="ReferenceTableId">The reference table to read.</param>
public sealed record GetReferenceValuesQuery(Guid ReferenceTableId) : IRequest<Result<IReadOnlyList<ReferenceValueDto>>>;

/// <summary>Handles <see cref="GetReferenceValuesQuery"/>.</summary>
/// <param name="referenceDataService">Reference data application service.</param>
public sealed class GetReferenceValuesQueryHandler(IReferenceDataService referenceDataService)
    : IRequestHandler<GetReferenceValuesQuery, Result<IReadOnlyList<ReferenceValueDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The reference values.</returns>
    public async Task<Result<IReadOnlyList<ReferenceValueDto>>> Handle(
        GetReferenceValuesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var values = await referenceDataService.GetReferenceValuesAsync(request.ReferenceTableId, cancellationToken);

        return Result.Success(values);
    }
}
