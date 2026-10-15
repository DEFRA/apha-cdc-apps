using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>How a call to CDC.Api's profile report endpoints turned out.</summary>
public enum ProfileReportsOutcome
{
    /// <summary>The call succeeded.</summary>
    Success,

    /// <summary>The profile version or report does not exist.</summary>
    NotFound,

    /// <summary>The user may not do this.</summary>
    Forbidden,

    /// <summary>The call failed for any other reason.</summary>
    Error
}

/// <summary>Result of <c>GET /api/profile-reports/overview/{profileVersionId}</c>.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Overview">The overview when <paramref name="Outcome"/> is <see cref="ProfileReportsOutcome.Success"/>.</param>
public sealed record ProfileReportsOverviewResult(ProfileReportsOutcome Outcome, ProfileReportsOverviewDto? Overview = null);

/// <summary>Result of <c>GET /api/profile-reports/content/{profileVersionId}</c>.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Content">The content when <paramref name="Outcome"/> is <see cref="ProfileReportsOutcome.Success"/>.</param>
public sealed record ProfileReportContentResult(ProfileReportsOutcome Outcome, ProfileReportContentDto? Content = null);

/// <summary>Result of <c>GET /api/profile-reports/prioritisation/{profileVersionId}</c>.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Scores">The scores when <paramref name="Outcome"/> is <see cref="ProfileReportsOutcome.Success"/>.</param>
public sealed record ProfilePrioritisationScoresResult(ProfileReportsOutcome Outcome, ProfilePrioritisationScoresDto? Scores = null);

/// <summary>Result of <c>POST /api/profile-reports/guidance</c>.</summary>
/// <param name="Outcome">The outcome classification.</param>
/// <param name="Guidance">The guidance when <paramref name="Outcome"/> is <see cref="ProfileReportsOutcome.Success"/>.</param>
public sealed record ProfileQuestionGuidanceResult(ProfileReportsOutcome Outcome, IReadOnlyList<ProfileQuestionGuidanceDto>? Guidance = null);

/// <summary>One row of the "Profile reports" table, with what the user may do with it.</summary>
/// <param name="Id">The report identifier.</param>
/// <param name="DisplayName">The report's display name.</param>
/// <param name="CanDownload">Whether a PDF has been generated and can be downloaded.</param>
/// <param name="CanRegenerate">Whether the user may regenerate it and a generator exists for it.</param>
public sealed record ProfileReportRow(Guid Id, string DisplayName, bool CanDownload, bool CanRegenerate);
