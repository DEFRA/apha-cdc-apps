using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Interfaces;
using MediatR;

namespace CDC.Api.Features.StaticReports.Commands;

/// <summary>Handles <see cref="UploadStaticReportCommand"/>.</summary>
/// <param name="repository">Static reports data access.</param>
public sealed class UploadStaticReportCommandHandler(IStaticReportRepository repository)
    : IRequestHandler<UploadStaticReportCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success once the new version is stored.</returns>
    public async Task<Result<Unit>> Handle(UploadStaticReportCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await repository.UploadAsync(request.Title, request.PdfData, request.IsUserManual, request.IsPublic, cancellationToken);

        return Result.Success(Unit.Value);
    }
}
