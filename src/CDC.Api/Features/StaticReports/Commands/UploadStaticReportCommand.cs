using CDC.Api.Domain.Common;
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
