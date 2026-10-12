namespace CDC.Web.Models;

/// <summary>One row for the "Contributions" tab: a user's logged contribution to the section.</summary>
public sealed record ProfileSectionContributionView(string FullName, string UserName, string OrganisationName, string LastContributionDate);

/// <summary>One row for the "Review comments" tab: a comment (or reply), indented by thread depth.</summary>
public sealed record ProfileReviewCommentView(string IndentedSubject, string CommentReference, string CommentText, string UserFullName, string CommentDate);

/// <summary>One row for the "All Revision dates" tab: one section's technical/policy review dates.</summary>
public sealed record ProfileSectionRevisionDatesView(
    string SectionDisplay,
    string TechnicalReviewFrequencyDisplay,
    string NextTechnicalReviewDisplay,
    string PolicyReviewFrequencyDisplay,
    string NextPolicyReviewDisplay);
