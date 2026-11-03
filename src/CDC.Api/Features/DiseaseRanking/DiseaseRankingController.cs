using CDC.Api.Application;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Common.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.DiseaseRanking;

/// <summary>
/// Disease ranking reports: the data and layout behind the legacy <c>DiseaseRanking.aspx</c>,
/// <c>ShowDiseaseRankingReport.aspx</c> and <c>ShowDiseaseRankingRFIReport.aspx</c> pages.
/// </summary>
/// <param name="diseaseRankingService">Disease ranking report service.</param>
[ApiController]
[Route("api/disease-ranking")]
[RequireGlobalRole(GlobalRole.ProfileEditor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class DiseaseRankingController(IDiseaseRankingService diseaseRankingService) : ControllerBase
{
    /// <summary>Gets the reports available to select.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The report options were retrieved.</response>
    [HttpGet("report-options")]
    [ProducesResponseType(typeof(IReadOnlyList<DiseaseRankingReportOptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DiseaseRankingReportOptionDto>>> GetReportOptions(CancellationToken cancellationToken) =>
        Ok(await diseaseRankingService.GetReportOptionsAsync(cancellationToken));

    /// <summary>Gets one report with its summary tables and chart data.</summary>
    /// <param name="reportKey"><c>terrestrial</c>, <c>aquatic</c> or a saved filter id from <see cref="GetReportOptions"/>.</param>
    /// <param name="kind">Scored or ranked graphs.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The report was built.</response>
    /// <response code="404">No report exists for <paramref name="reportKey"/>.</response>
    [HttpGet("reports/{reportKey}")]
    [ProducesResponseType(typeof(DiseaseRankingReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiseaseRankingReportDto>> GetReport(
        string reportKey,
        [FromQuery] DiseaseRankingReportKind kind = DiseaseRankingReportKind.Scored,
        CancellationToken cancellationToken = default)
    {
        var report = await diseaseRankingService.GetReportAsync(reportKey, kind, cancellationToken);

        return report is null ? NotFound() : Ok(report);
    }
}
