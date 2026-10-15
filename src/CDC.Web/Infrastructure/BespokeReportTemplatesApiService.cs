using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Default <see cref="IBespokeReportTemplatesApiService"/>.</summary>
/// <param name="httpClient">The CDC.Api client.</param>
public sealed class BespokeReportTemplatesApiService(HttpClient httpClient) : IBespokeReportTemplatesApiService
{
    private const string BasePath = "/api/bespoke-report-templates";

    /// <inheritdoc />
    public async Task<BespokeTemplateListResult> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BasePath, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new BespokeTemplateListResult(ToOutcome(response.StatusCode));
        }

        var templates = await response.Content.ReadFromJsonAsync<List<BespokeReportTemplateSummaryDto>>(cancellationToken);

        return templates is null
            ? new BespokeTemplateListResult(BespokeTemplatesOutcome.Error)
            : new BespokeTemplateListResult(BespokeTemplatesOutcome.Success, templates);
    }

    /// <inheritdoc />
    public async Task<BespokeTemplateResult> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{BasePath}/{id}", cancellationToken);

        return await ReadTemplateAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BespokeTemplateResult> SaveAsAsync(string title, IReadOnlyList<string> checkedList, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            BasePath,
            new SaveBespokeReportTemplateRequest { Title = title, CheckedList = checkedList },
            cancellationToken);

        return await ReadTemplateAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BespokeTemplateResult> UpdateAsync(Guid id, IReadOnlyList<string> checkedList, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"{BasePath}/{id}",
            new UpdateBespokeReportTemplateRequest { CheckedList = checkedList },
            cancellationToken);

        return await ReadTemplateAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BespokeTemplateResult> RenameAsync(Guid id, string title, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"{BasePath}/{id}/title",
            new RenameBespokeReportTemplateRequest { Title = title },
            cancellationToken);

        return await ReadTemplateAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BespokeTemplatesOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{BasePath}/{id}", cancellationToken);

        return response.IsSuccessStatusCode ? BespokeTemplatesOutcome.Success : ToOutcome(response.StatusCode);
    }

    private static async Task<BespokeTemplateResult> ReadTemplateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return new BespokeTemplateResult(ToOutcome(response.StatusCode));
        }

        var template = await response.Content.ReadFromJsonAsync<BespokeReportTemplateDto>(cancellationToken);

        return template is null
            ? new BespokeTemplateResult(BespokeTemplatesOutcome.Error)
            : new BespokeTemplateResult(BespokeTemplatesOutcome.Success, template);
    }

    private static BespokeTemplatesOutcome ToOutcome(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => BespokeTemplatesOutcome.NotFound,
        HttpStatusCode.Forbidden => BespokeTemplatesOutcome.Forbidden,
        HttpStatusCode.Conflict => BespokeTemplatesOutcome.Conflict,
        HttpStatusCode.BadRequest => BespokeTemplatesOutcome.Invalid,
        _ => BespokeTemplatesOutcome.Error
    };
}
