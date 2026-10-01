using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Interfaces;
using MediatR;

namespace CDC.Api.Features.StaticReports.Commands;

/// <summary>Handles <see cref="DeleteStaticReportVersionCommand"/>.</summary>
/// <param name="repository">Static reports data access.</param>
public sealed class DeleteStaticReportVersionCommandHandler(IStaticReportRepository repository)
    : IRequestHandler<DeleteStaticReportVersionCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the version is removed.</returns>
    public async Task<Result<Unit>> Handle(DeleteStaticReportVersionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await repository.DeleteAsync(request.StaticReportVersionId, cancellationToken);

        return Result.Success(Unit.Value);
    }
}
