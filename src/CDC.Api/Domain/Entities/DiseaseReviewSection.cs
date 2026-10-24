namespace CDC.Api.Domain.Entities;

/// <summary>One profile section with a review due, as read from <c>spgaDiseaseReviews</c>. The database stores the
/// VA review in its legacy "Technical" columns.</summary>
public sealed record DiseaseReviewSection
{
    /// <summary>Gets the identifier of the profile version the section belongs to.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets the profile version's title.</summary>
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
