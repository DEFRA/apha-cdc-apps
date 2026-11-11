using CDC.Api.Domain.Common;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using MediatR;

namespace CDC.Api.Features.ReferenceData.Queries;

/// <summary>Retrieves the audit trail of one maintainable reference table, most recent change first.</summary>
/// <param name="ReferenceTableId">The table to read.</param>
public sealed record GetReferenceTableAuditTrailQuery(Guid ReferenceTableId)
    : IRequest<Result<IReadOnlyList<ReferenceTableAuditEntryDto>>>;

/// <summary>Handles <see cref="GetReferenceTableAuditTrailQuery"/>.</summary>
/// <param name="repository">Reference data maintenance access.</param>
public sealed class GetReferenceTableAuditTrailQueryHandler(IReferenceDataMaintenanceRepository repository)
    : IRequestHandler<GetReferenceTableAuditTrailQuery, Result<IReadOnlyList<ReferenceTableAuditEntryDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The audit entries, or not found when the table is not maintainable.</returns>
    public async Task<Result<IReadOnlyList<ReferenceTableAuditEntryDto>>> Handle(
        GetReferenceTableAuditTrailQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tables = await repository.GetMaintainableTablesAsync(cancellationToken);
        var table = tables.FirstOrDefault(candidate => candidate.Id == request.ReferenceTableId);

        if (table is null)
        {
            return Result.NotFound<IReadOnlyList<ReferenceTableAuditEntryDto>>("The reference table does not exist or is not maintainable.");
        }

        var entries = await repository.GetAuditTrailAsync(table.Id, cancellationToken);

        return Result.Success<IReadOnlyList<ReferenceTableAuditEntryDto>>([.. entries.Select(entry => entry with { TableName = table.Name })]);
    }
}
