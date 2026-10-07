using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IStaticReportsApiService"/>. The base address is configured on the
/// underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is
/// hardcoded here.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class StaticReportsApiService(HttpClient httpClient) : IStaticReportsApiService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportVersionDto>> GetCurrentAsync(
        bool isUserManual,
        CancellationToken cancellationToken = default)
    {
        var reports = await httpClient.GetFromJsonAsync<IReadOnlyList<StaticReportVersionDto>>(
            $"/api/static-reports?isUserManual={isUserManual}", cancellationToken);

        return reports ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportVersionDto>> GetHistoryAsync(
        Guid staticReportId,
        CancellationToken cancellationToken = default)
    {
        var versions = await httpClient.GetFromJsonAsync<IReadOnlyList<StaticReportVersionDto>>(
            $"/api/static-reports/{staticReportId}/history", cancellationToken);

        return versions ?? [];
    }

    /// <inheritdoc />
    public async Task<StaticReportDataDto?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/static-reports/{staticReportVersionId}/data", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StaticReportDataDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StaticReportUpdateResult> UploadAsync(
        UploadStaticReportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/static-reports", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new StaticReportUpdateResult { Outcome = StaticReportUpdateOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new StaticReportUpdateResult
            {
                Outcome = StaticReportUpdateOutcome.ValidationFailed,
                ErrorMessage = "The uploaded document must be a Pdf with a title."
            },
            _ => new StaticReportUpdateResult
            {
                Outcome = StaticReportUpdateOutcome.Error,
                ErrorMessage = "We could not upload this document. Try again later."
            }
        };
    }

    /// <inheritdoc />
    public async Task<StaticReportUpdateResult> DeleteAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"/api/static-reports/{staticReportVersionId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new StaticReportUpdateResult { Outcome = StaticReportUpdateOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new StaticReportUpdateResult
            {
                Outcome = StaticReportUpdateOutcome.Conflict,
                ErrorMessage = "This document cannot be deleted because it is no longer the current version."
            },
            HttpStatusCode.BadRequest => new StaticReportUpdateResult
            {
                Outcome = StaticReportUpdateOutcome.ValidationFailed,
                ErrorMessage = "The document could not be identified."
            },
            _ => new StaticReportUpdateResult
            {
                Outcome = StaticReportUpdateOutcome.Error,
                ErrorMessage = "We could not delete this document. Try again later."
            }
        };
    }
}
