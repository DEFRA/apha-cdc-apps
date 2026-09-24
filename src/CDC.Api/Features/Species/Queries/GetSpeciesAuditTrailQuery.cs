using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using MediatR;

namespace CDC.Api.Features.Species.Queries;

/// <summary>
/// Retrieves every recorded species name/parent change, most recent first.
/// </summary>
public sealed record GetSpeciesAuditTrailQuery : IRequest<Result<IReadOnlyList<SpeciesAuditTrailEntryDto>>>;

/// <summary>
/// Handles <see cref="GetSpeciesAuditTrailQuery"/>.
/// </summary>
/// <param name="speciesService">Species application service.</param>
public sealed class GetSpeciesAuditTrailQueryHandler(ISpeciesService speciesService)
    : IRequestHandler<GetSpeciesAuditTrailQuery, Result<IReadOnlyList<SpeciesAuditTrailEntryDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The audit trail, most recent entry first.</returns>
    public async Task<Result<IReadOnlyList<SpeciesAuditTrailEntryDto>>> Handle(
        GetSpeciesAuditTrailQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entries = await speciesService.GetSpeciesAuditTrailAsync(cancellationToken);

        return Result.Success(entries);
    }
}
