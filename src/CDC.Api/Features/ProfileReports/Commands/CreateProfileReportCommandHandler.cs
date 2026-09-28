using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Commands;

/// <summary>Handles <see cref="CreateProfileReportCommand"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class CreateProfileReportCommandHandler(IProfileReportService profileReportService)
    : IRequestHandler<CreateProfileReportCommand, Result<CreateProfileReportResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The report to persist.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The persisted report's identifier.</returns>
    public async Task<Result<CreateProfileReportResultDto>> Handle(CreateProfileReportCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileReportService.CreateProfileReportAsync(request, cancellationToken);

        return Result.Success(result);
    }
}
