namespace CDC.Api.Domain.Entities;

/// <summary>A saved bespoke report template, read from <c>BespokeReportTemplate</c> and <c>BespokeReportTemplateCheckedItems</c>.</summary>
public sealed record BespokeReportTemplate
{
    /// <summary>Gets the template's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the template's title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ticked tree nodes, each a node's identifier followed by its type's number (0 profile,
    /// 1 section, 2 question, 3 guidance). Empty when only the title was read.
    /// </summary>
    public IReadOnlyList<string> CheckedItems { get; init; } = [];
}
