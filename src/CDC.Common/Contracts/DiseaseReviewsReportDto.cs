namespace CDC.Common.Contracts;

/// <summary>
/// The data behind the disease reviews report, as returned by <c>GET /api/profile-reports/disease-reviews</c>:
/// the profile sections whose author, VA or policy review falls due in a date range for one user. Replaces the
/// <c>DiseaseReviewsData</c> rows the legacy <c>DiseaseReviewsReportPdf</c> document printed.
/// </summary>
public sealed record DiseaseReviewsReportDto
{
    /// <summary>Gets one row per profile section with a review due, in the order the database returned them: by profile, then section number.</summary>
    public IReadOnlyList<DiseaseReviewsReportRowDto> Sections { get; init; } = [];
}

/// <summary>One profile section with a review due in the report's date range.</summary>
public sealed record DiseaseReviewsReportRowDto
{
    /// <summary>Gets the identifier of the profile version the section belongs to.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets the profile version's title. May contain HTML.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the major version number.</summary>
    public int VersionMajor { get; init; }

    /// <summary>Gets the minor version number.</summary>
    public int VersionMinor { get; init; }

    /// <summary>Gets the section's number.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the section's name.</summary>
    public string SectionName { get; init; } = string.Empty;

    /// <summary>Gets the date the next policy review is due, or <see langword="null"/> when none is set.</summary>
    public DateTime? NextPolicyReview { get; init; }

    /// <summary>Gets the date the next author review is due, or <see langword="null"/> when none is set.</summary>
    public DateTime? NextAuthorReview { get; init; }

    /// <summary>Gets the date the next VA review is due, or <see langword="null"/> when none is set.</summary>
    public DateTime? NextVaReview { get; init; }
}
