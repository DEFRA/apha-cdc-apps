using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

/// <summary>Scriptable <see cref="IBespokeReportTemplatesApiService"/> that records what it was asked to do.</summary>
internal sealed class FakeBespokeReportTemplatesApiService : IBespokeReportTemplatesApiService
{
    /// <summary>Gets or sets what the list call returns.</summary>
    public BespokeTemplateListResult ListResult { get; set; } = new(BespokeTemplatesOutcome.Success, []);

    /// <summary>Gets or sets what the single-template call returns.</summary>
    public BespokeTemplateResult GetResult { get; set; } = new(BespokeTemplatesOutcome.NotFound);

    /// <summary>Gets or sets what "save as" returns.</summary>
    public BespokeTemplateResult SaveAsResult { get; set; } = new(BespokeTemplatesOutcome.Success, new BespokeReportTemplateDto { Id = Guid.NewGuid(), Title = "Saved" });

    /// <summary>Gets or sets what "update" returns.</summary>
    public BespokeTemplateResult UpdateResult { get; set; } = new(BespokeTemplatesOutcome.Success, new BespokeReportTemplateDto { Id = Guid.NewGuid(), Title = "Updated" });

    /// <summary>Gets or sets what "rename" returns.</summary>
    public BespokeTemplateResult RenameResult { get; set; } = new(BespokeTemplatesOutcome.Success, new BespokeReportTemplateDto { Id = Guid.NewGuid(), Title = "Renamed" });

    /// <summary>Gets or sets what "delete" returns.</summary>
    public BespokeTemplatesOutcome DeleteOutcome { get; set; } = BespokeTemplatesOutcome.Success;

    /// <summary>Gets or sets an exception every call throws.</summary>
    public Exception? Throw { get; set; }

    /// <summary>Gets or sets an exception only the save, update, rename and delete calls throw, so the page can still be shown afterwards.</summary>
    public Exception? ThrowOnWrite { get; set; }

    /// <summary>Gets the template id the single-template call was given.</summary>
    public Guid? RequestedTemplateId { get; private set; }

    /// <summary>Gets the title and ticked items "save as" was given.</summary>
    public (string Title, IReadOnlyList<string> CheckedList)? SavedAs { get; private set; }

    /// <summary>Gets the template id and ticked items "update" was given.</summary>
    public (Guid Id, IReadOnlyList<string> CheckedList)? Updated { get; private set; }

    /// <summary>Gets the template id and title "rename" was given.</summary>
    public (Guid Id, string Title)? Renamed { get; private set; }

    /// <summary>Gets the template id "delete" was given.</summary>
    public Guid? Deleted { get; private set; }

    public Task<BespokeTemplateListResult> GetTemplatesAsync(CancellationToken cancellationToken = default) =>
        Throw is not null ? throw Throw : Task.FromResult(ListResult);

    public Task<BespokeTemplateResult> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequestedTemplateId = id;

        return Throw is not null ? throw Throw : Task.FromResult(GetResult);
    }

    public Task<BespokeTemplateResult> SaveAsAsync(string title, IReadOnlyList<string> checkedList, CancellationToken cancellationToken = default)
    {
        SavedAs = (title, checkedList);

        return ThrowOnWrite is not null ? throw ThrowOnWrite : Task.FromResult(SaveAsResult);
    }

    public Task<BespokeTemplateResult> UpdateAsync(Guid id, IReadOnlyList<string> checkedList, CancellationToken cancellationToken = default)
    {
        Updated = (id, checkedList);

        return ThrowOnWrite is not null ? throw ThrowOnWrite : Task.FromResult(UpdateResult);
    }

    public Task<BespokeTemplateResult> RenameAsync(Guid id, string title, CancellationToken cancellationToken = default)
    {
        Renamed = (id, title);

        return ThrowOnWrite is not null ? throw ThrowOnWrite : Task.FromResult(RenameResult);
    }

    public Task<BespokeTemplatesOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Deleted = id;

        return ThrowOnWrite is not null ? throw ThrowOnWrite : Task.FromResult(DeleteOutcome);
    }
}

/// <summary>Scriptable <see cref="IBespokeReportPdfBuilder"/> that records what it was asked to build.</summary>
internal sealed class FakeBespokeReportPdfBuilder(byte[]? pdf = null, Exception? throwOnBuild = null) : IBespokeReportPdfBuilder
{
    /// <summary>Gets the profile version the report was built for.</summary>
    public Guid? ProfileVersionId { get; private set; }

    /// <summary>Gets the selection the report was built for.</summary>
    public BespokeReportSelection? Selection { get; private set; }

    /// <summary>Gets the template title the report was built under.</summary>
    public string? TemplateTitle { get; private set; }

    public Task<byte[]> BuildAsync(Guid profileVersionId, BespokeReportSelection selection, string? templateTitle, CancellationToken cancellationToken)
    {
        ProfileVersionId = profileVersionId;
        Selection = selection;
        TemplateTitle = templateTitle;

        return throwOnBuild is not null ? throw throwOnBuild : Task.FromResult(pdf ?? "%PDF-1.7 bespoke"u8.ToArray());
    }
}
