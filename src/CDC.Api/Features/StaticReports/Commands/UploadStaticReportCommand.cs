using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Interfaces;
using MediatR;

namespace CDC.Api.Features.StaticReports.Commands;

/// <summary>
/// Uploads a new static report version, superseding the previous current version for that
/// title - mirrors the legacy <c>UploadStaticReportCommand</c> business rule.
/// </summary>
/// <param name="Title">The report title. An existing report with this title gains a new version.</param>
/// <param name="PdfData">The PDF bytes to store.</param>
/// <param name="IsUserManual">Whether this is a user manual rather than a general report.</param>
/// <param name="IsPublic">Whether this version is visible to unauthenticated users.</param>
public sealed record UploadStaticReportCommand(string Title, byte[] PdfData, bool IsUserManual, bool IsPublic)
    : IRequest<Result<Unit>>;

/// <summary>
/// Handles <see cref="UploadStaticReportCommand"/>.
/// </summary>
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
        await repository.UploadAsync(request.Title, request.PdfData, request.IsUserManual, request.IsPublic, cancellationToken);

        return Result.Success(Unit.Value);
    }
}
