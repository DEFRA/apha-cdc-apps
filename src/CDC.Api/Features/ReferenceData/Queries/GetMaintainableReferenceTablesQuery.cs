using CDC.Api.Domain.Common;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using MediatR;

namespace CDC.Api.Features.ReferenceData.Queries;

/// <summary>Retrieves the reference tables a profile editor can maintain.</summary>
public sealed record GetMaintainableReferenceTablesQuery : IRequest<Result<IReadOnlyList<MaintainableReferenceTableDto>>>;

/// <summary>Handles <see cref="GetMaintainableReferenceTablesQuery"/>.</summary>
/// <param name="repository">Reference data maintenance access.</param>
public sealed class GetMaintainableReferenceTablesQueryHandler(IReferenceDataMaintenanceRepository repository)
    : IRequestHandler<GetMaintainableReferenceTablesQuery, Result<IReadOnlyList<MaintainableReferenceTableDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The maintainable tables.</returns>
    public async Task<Result<IReadOnlyList<MaintainableReferenceTableDto>>> Handle(
        GetMaintainableReferenceTablesQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await repository.GetMaintainableTablesAsync(cancellationToken));
}
