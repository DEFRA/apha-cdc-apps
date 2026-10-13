using System.Globalization;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// The Review comments tab of a profile section: its comments and replies. Replaces the legacy
/// <c>EditProfileReviewComments.aspx</c>. Shown only to users allowed to see it (legacy
/// <c>ReviewCommentList.CanGetReviewCommentList()</c>).
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="profileSectionsApiService">Typed client for the profile sections endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class EditProfileReviewCommentsModel(
    IApiClient apiClient,
    IProfileSectionsApiService profileSectionsApiService,
    ILogger<EditProfileReviewCommentsModel> logger) : ProfileSectionTabPageModel(apiClient, profileSectionsApiService, logger)
{
    /// <inheritdoc />
    public override ProfileSectionTab ActiveTab => ProfileSectionTab.ReviewComments;

    /// <summary>Gets the selected section's review comments, each reply indented by its thread depth.</summary>
    public IReadOnlyList<ProfileReviewCommentView> ReviewComments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var notFound = await LoadProfileContextAsync(cancellationToken);

        if (notFound is not null)
        {
            return notFound;
        }

        if (!IsReady)
        {
            return Page();
        }

        if (!CanViewReviewComments)
        {
            return NotFound();
        }

        await LoadReviewCommentsAsync(cancellationToken);

        return Page();
    }

    /// <summary>Loads the comments, resiliently: a failure degrades to an empty list rather than failing the page.</summary>
    private async Task LoadReviewCommentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var comments = await ProfileSectionsApi.GetProfileSectionReviewCommentsAsync(CurrentProfileVersionId, CurrentSection!.Id, cancellationToken);

            ReviewComments =
            [
                .. comments.Select(comment => new ProfileReviewCommentView(
                    IndentSubject(comment.Level) + comment.CommentSubject,
                    comment.CommentReference,
                    comment.CommentText,
                    comment.UserFullName,
                    comment.CommentDate.ToString(DateDisplayFormat, CultureInfo.InvariantCulture)))
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            Logger.ProfileReviewCommentsLoadFailed(exception, ProfileId);
        }
    }

    /// <summary>Indents a reply's subject by its thread depth, matching the legacy
    /// <c>EditProfileReviewComments.aspx.vb</c>'s <c>IndentSubject()</c>.</summary>
    private static string IndentSubject(int level) =>
        level <= 1 ? string.Empty : new string('\u00A0', (level - 1) * 5);
}
