namespace CDC.Common.Contracts;

/// <summary>A saved bespoke report template without its selections, as listed in the page's template drop-down.</summary>
public sealed record BespokeReportTemplateSummaryDto
{
    /// <summary>Gets the template's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the template's title.</summary>
    public string Title { get; init; } = string.Empty;
}

/// <summary>A saved bespoke report template: a title and the tree nodes that were ticked when it was saved.</summary>
public sealed record BespokeReportTemplateDto
{
    /// <summary>Gets the template's identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the template's title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ticked tree nodes. Each is a node's identifier followed by its type's number: 0 profile,
    /// 1 section, 2 question, 3 guidance (the format the legacy page stored).
    /// </summary>
    public IReadOnlyList<string> CheckedList { get; init; } = [];
}

/// <summary>Body of <c>POST api/bespoke-report-templates</c>: "Save template as" a new template.</summary>
public sealed record SaveBespokeReportTemplateRequest
{
    /// <summary>Gets the new template's title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the ticked tree nodes to save.</summary>
    public IReadOnlyList<string> CheckedList { get; init; } = [];
}

/// <summary>Body of <c>PUT api/bespoke-report-templates/{id}</c>: "Update template" replaces its ticked nodes.</summary>
public sealed record UpdateBespokeReportTemplateRequest
{
    /// <summary>Gets the ticked tree nodes to save.</summary>
    public IReadOnlyList<string> CheckedList { get; init; } = [];
}

/// <summary>Body of <c>PUT api/bespoke-report-templates/{id}/title</c>: "Edit title".</summary>
public sealed record RenameBespokeReportTemplateRequest
{
    /// <summary>Gets the template's new title.</summary>
    public string Title { get; init; } = string.Empty;
}

/// <summary>Body of <c>POST api/profile-reports/guidance</c>.</summary>
public sealed record ProfileQuestionGuidanceRequest
{
    /// <summary>Gets the questions whose guidance is wanted.</summary>
    public IReadOnlyList<Guid> QuestionIds { get; init; } = [];
}

/// <summary>One question's user guidance, as printed by the bespoke report's guidance tables.</summary>
public sealed record ProfileQuestionGuidanceDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid QuestionId { get; init; }

    /// <summary>Gets the question's guidance text; may contain HTML. Empty when the question has none.</summary>
    public string UserGuidance { get; init; } = string.Empty;
}
