using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>The kinds of node in the "Create bespoke report" tree, numbered as the legacy <c>ProfileNodeTypeEnum</c> was.</summary>
public enum BespokeNodeType
{
    /// <summary>The profile, the root of the tree.</summary>
    Profile = 0,

    /// <summary>A profile section.</summary>
    Section = 1,

    /// <summary>A question's answers.</summary>
    Question = 2,

    /// <summary>A question's guidance.</summary>
    Guidance = 3
}

/// <summary>The sections, questions and guidance ticked on the "Create bespoke report" tree.</summary>
/// <param name="Sections">The ticked sections, which are the only ones printed.</param>
/// <param name="Questions">The ticked questions, whose answer tables are printed.</param>
/// <param name="Guidance">The questions whose guidance tables are printed.</param>
public sealed record BespokeReportSelection(IReadOnlySet<Guid> Sections, IReadOnlySet<Guid> Questions, IReadOnlySet<Guid> Guidance);

/// <summary>How a call to CDC.Api's bespoke report template endpoints turned out.</summary>
public enum BespokeTemplatesOutcome
{
    /// <summary>The call succeeded.</summary>
    Success,

    /// <summary>The template does not exist.</summary>
    NotFound,

    /// <summary>The user may not do this.</summary>
    Forbidden,

    /// <summary>Another template already has the title.</summary>
    Conflict,

    /// <summary>The request failed validation.</summary>
    Invalid,

    /// <summary>The call failed for any other reason.</summary>
    Error
}

/// <summary>Result of <c>GET /api/bespoke-report-templates</c>.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Templates">The templates when <paramref name="Outcome"/> is <see cref="BespokeTemplatesOutcome.Success"/>.</param>
public sealed record BespokeTemplateListResult(BespokeTemplatesOutcome Outcome, IReadOnlyList<BespokeReportTemplateSummaryDto>? Templates = null);

/// <summary>Result of reading or saving one bespoke report template.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Template">The template when <paramref name="Outcome"/> is <see cref="BespokeTemplatesOutcome.Success"/>.</param>
public sealed record BespokeTemplateResult(BespokeTemplatesOutcome Outcome, BespokeReportTemplateDto? Template = null);
