using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditProfileReviewCommentsModelTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 13, 12, 0, 0);
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static (EditProfileReviewCommentsModel PageModel, FakeProfileSectionsApiService Api) CreatePage(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileReviewCommentDto>? reviewComments = null,
        Exception? throwOnGetProfileSectionReviewComments = null,
        ReviewCommentResult? reviewCommentResult = null,
        Exception? throwOnChangeReviewComment = null)
    {
        var api = new FakeProfileSectionsApiService(
            metadata: TwoSectionMetadata(),
            reviewComments: reviewComments,
            throwOnGetProfileSectionReviewComments: throwOnGetProfileSectionReviewComments,
            reviewCommentResult: reviewCommentResult,
            throwOnChangeReviewComment: throwOnChangeReviewComment);

        var pageModel = Configure(new EditProfileReviewCommentsModel(
            new FakeApiClient(manageProfile: manageProfile),
            api,
            new AlwaysEnabledLogger<EditProfileReviewCommentsModel>(),
            new FixedTimeProvider(FixedNow)));

        return (pageModel, api);
    }

    private static ProfileReviewCommentDto Comment(
        string subject = "Top level",
        int level = 1,
        bool canEdit = false,
        DateTime? date = null,
        string reference = "Ref 1",
        string text = "<p>Body</p>")
    {
        return new ProfileReviewCommentDto
        {
            Id = Guid.NewGuid(),
            CommentSubject = subject,
            CommentReference = reference,
            CommentText = text,
            UserFullName = "Jane Doe",
            Level = level,
            CanEdit = canEdit,
            CanDelete = canEdit,
            LastUpdated = RowVersion,
            CommentDate = date ?? new DateTime(2026, 9, 11, 13, 14, 15)
        };
    }

    [Fact]
    public void ActiveTab_IsReviewComments() =>
        Assert.Equal(ProfileSectionTab.ReviewComments, CreatePage(Profile()).PageModel.ActiveTab);

    [Fact]
    public async Task OnGetAsync_ListsComments_WithDateAndTimeAndThreadLevel()
    {
        var (pageModel, _) = CreatePage(DraftProfile(), [Comment(), Comment("A reply", level: 2)]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.ReviewComments.Count);
        Assert.Equal("Top level", pageModel.ReviewComments[0].DisplaySubject);
        Assert.Equal("11/09/26 13:14:15", pageModel.ReviewComments[0].DateDisplay);
        Assert.Equal("Jane Doe", pageModel.ReviewComments[0].Reviewer);
        Assert.Equal(2, pageModel.ReviewComments[1].Level);
    }

    [Fact]
    public async Task OnGetAsync_TruncatesLongSubjects_ToFiftyCharactersPlusEllipsis()
    {
        var (pageModel, _) = CreatePage(DraftProfile(), [Comment(new string('x', 60))]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(new string('x', 50) + "...", pageModel.ReviewComments[0].DisplaySubject);
    }

    [Fact]
    public async Task OnGetAsync_FlagsCommentsFromTheLastTwentyFourHoursAsNew()
    {
        var (pageModel, _) = CreatePage(
            DraftProfile(),
            [Comment("Fresh", date: FixedNow.AddHours(-2)), Comment("Old", date: FixedNow.AddHours(-30))]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.ReviewComments[0].IsNew);
        Assert.False(pageModel.ReviewComments[1].IsNew);
    }

    [Fact]
    public async Task OnGetAsync_PagesTheComments_AndClampsAnOutOfRangePage()
    {
        var comments = Enumerable.Range(1, 12).Select(index => Comment($"Comment {index}")).ToList();
        var (pageModel, _) = CreatePage(DraftProfile(), comments);
        pageModel.PageNumber = 99;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(12, pageModel.TotalRecords);
        Assert.Equal(2, pageModel.TotalPages);
        Assert.Equal(2, pageModel.PageNumber);
        Assert.Equal(2, pageModel.ReviewComments.Count);
    }

    [Fact]
    public async Task OnGetAsync_UsesTheChosenPageSize_AndFallsBackToTenForAnInvalidOne()
    {
        var comments = Enumerable.Range(1, 12).Select(index => Comment($"Comment {index}")).ToList();
        var (sized, _) = CreatePage(DraftProfile(), comments);
        sized.PageSize = "25";
        var (invalid, _) = CreatePage(DraftProfile(), comments);
        invalid.PageSize = "abc";

        await sized.OnGetAsync(CancellationToken.None);
        await invalid.OnGetAsync(CancellationToken.None);

        Assert.Equal(12, sized.ReviewComments.Count);
        Assert.Equal(10, invalid.ReviewComments.Count);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheUserCannotViewReviewComments()
    {
        var (pageModel, _) = CreatePage(Profile(), [Comment()]);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(pageModel.ReviewComments);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotFail_WhenLoadingReviewCommentsThrows()
    {
        var (pageModel, _) = CreatePage(DraftProfile(), throwOnGetProfileSectionReviewComments: new HttpRequestException("boom"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Empty(pageModel.ReviewComments);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsThePage_WhenTheProfileHasNoVersion()
    {
        var (pageModel, _) = CreatePage(DraftProfile() with { CurrentProfileVersionId = Guid.Empty });

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasNoVersion);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var (pageModel, _) = CreatePage(manageProfile: null);

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_OpensTheViewPanel_ForASelectedComment_WithSanitisedHtml()
    {
        var comment = Comment(text: "<p>Hello <a href=\"https://example.com\">link</a></p><script>alert(1)</script>");
        var (pageModel, _) = CreatePage(DraftProfile(), [comment]);
        pageModel.CommentId = comment.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ReviewCommentPanel.View, pageModel.Panel);
        Assert.Equal("Top level", pageModel.Detail!.Subject);
        Assert.Contains("<a href=\"https://example.com\">link</a>", pageModel.Detail.CommentHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("script", pageModel.Detail.CommentHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnGetAsync_ShowsNoDetailPanel_WhenTheSelectedCommentDoesNotExist()
    {
        var (pageModel, _) = CreatePage(DraftProfile(), [Comment()]);
        pageModel.CommentId = Guid.NewGuid();

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ReviewCommentPanel.None, pageModel.Panel);
    }

    [Fact]
    public async Task OnGetAsync_OffersEditAndDelete_OnlyWhenTheApiSaysTheUserCan()
    {
        var own = Comment(canEdit: true);
        var other = Comment("Someone else's");
        var (pageModel, _) = CreatePage(DraftProfile(), [own, other]);
        pageModel.CommentId = own.Id;
        var (otherPage, _) = CreatePage(DraftProfile(), [own, other]);
        otherPage.CommentId = other.Id;

        await pageModel.OnGetAsync(CancellationToken.None);
        await otherPage.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.Detail!.CanEdit);
        Assert.True(pageModel.Detail.CanDelete);
        Assert.False(otherPage.Detail!.CanEdit);
        Assert.False(otherPage.Detail.CanDelete);
    }

    [Fact]
    public async Task OnGetAsync_OpensTheAddPanel_WhenTheUserCanEdit()
    {
        var (pageModel, _) = CreatePage(DraftProfile());
        pageModel.Mode = "add";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.CanAdd);
        Assert.Equal(ReviewCommentPanel.Add, pageModel.Panel);
        Assert.Equal("Add review comment", pageModel.PanelHeading);
        Assert.Equal(string.Empty, pageModel.Input.Subject);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotOpenTheAddPanel_WhenTheUserCannotEdit()
    {
        var (pageModel, _) = CreatePage(DraftProfile() with { CanEditProfileSection = false });
        pageModel.Mode = "add";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.CanAdd);
        Assert.Equal(ReviewCommentPanel.None, pageModel.Panel);
    }

    [Theory]
    [InlineData("Original subject", "Re: Original subject")]
    [InlineData("Re: Original subject", "Re: Original subject")]
    [InlineData("RE: shouting", "RE: shouting")]
    public async Task OnGetAsync_Reply_PrefillsTheSubjectAndReference(string parentSubject, string expectedSubject)
    {
        var parent = Comment(parentSubject, reference: "Parent ref");
        var (pageModel, _) = CreatePage(DraftProfile(), [parent]);
        pageModel.Mode = "reply";
        pageModel.CommentId = parent.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ReviewCommentPanel.Add, pageModel.Panel);
        Assert.Equal(expectedSubject, pageModel.Input.Subject);
        Assert.Equal("Parent ref", pageModel.Input.Reference);
        Assert.Equal(string.Empty, pageModel.Input.CommentText);
        Assert.Equal(parent.Id, pageModel.Input.ParentId);
    }

    [Fact]
    public async Task OnGetAsync_Reply_OpensNothing_WhenTheParentDoesNotExist()
    {
        var (pageModel, _) = CreatePage(DraftProfile(), [Comment()]);
        pageModel.Mode = "reply";
        pageModel.CommentId = Guid.NewGuid();

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ReviewCommentPanel.None, pageModel.Panel);
    }

    [Fact]
    public async Task OnGetAsync_Edit_PrefillsTheComment_WhenItIsEditable()
    {
        var comment = Comment(canEdit: true);
        var (pageModel, _) = CreatePage(DraftProfile(), [comment]);
        pageModel.Mode = "edit";
        pageModel.CommentId = comment.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ReviewCommentPanel.Edit, pageModel.Panel);
        Assert.Equal("Edit review comment", pageModel.PanelHeading);
        Assert.Equal("Top level", pageModel.Input.Subject);
        Assert.Equal("<p>Body</p>", pageModel.Input.CommentText);
        Assert.Equal(comment.Id, pageModel.Input.CommentId);
        Assert.Equal(Convert.ToBase64String(RowVersion), pageModel.Input.LastUpdated);
    }

    [Fact]
    public async Task OnGetAsync_Edit_OpensNothing_WhenTheCommentIsNotEditable()
    {
        var comment = Comment();
        var (pageModel, _) = CreatePage(DraftProfile(), [comment]);
        pageModel.Mode = "edit";
        pageModel.CommentId = comment.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ReviewCommentPanel.None, pageModel.Panel);
    }

    [Theory]
    [InlineData("saved", "Review comment save succeeded")]
    [InlineData("deleted", "Review comment deletion succeeded")]
    public async Task OnGetAsync_ShowsTheResultOfThePreviousChange(string notice, string expectedMessage)
    {
        var (pageModel, _) = CreatePage(DraftProfile());
        pageModel.Notice = notice;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedMessage, pageModel.ActionMessage);
        Assert.False(pageModel.ActionMessageIsError);
    }

    [Fact]
    public async Task OnGetAsync_IgnoresAnUnknownResultNotice()
    {
        var (pageModel, _) = CreatePage(DraftProfile());
        pageModel.Notice = "<script>";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Null(pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReportsEveryMissingField()
    {
        var (pageModel, api) = CreatePage(DraftProfile());
        pageModel.Input = new ReviewCommentInput { Subject = "  ", Reference = string.Empty, CommentText = "<p></p>" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            ["You must enter a subject or cancel", "You must enter a reference or cancel", "You must enter a comment or cancel"],
            pageModel.ValidationErrors);
        Assert.Equal(ReviewCommentPanel.Add, pageModel.Panel);
        Assert.Null(api.AddedReviewComment);
    }

    [Fact]
    public async Task OnPostSaveAsync_RejectsASubjectAndReferenceOverTwoHundredAndFiftyFiveCharacters()
    {
        var (pageModel, api) = CreatePage(DraftProfile());
        pageModel.Input = new ReviewCommentInput { Subject = new string('s', 256), Reference = new string('r', 256), CommentText = "Body" };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal(
            ["A subject must be no longer than 255 characters", "A reference must be no longer than 255 characters"],
            pageModel.ValidationErrors);
        Assert.Null(api.AddedReviewComment);
    }

    [Fact]
    public async Task OnPostSaveAsync_AddsTheComment_AndRedirectsWithASavedNotice()
    {
        var (pageModel, api) = CreatePage(DraftProfile());
        var parentId = Guid.NewGuid();
        pageModel.Input = new ReviewCommentInput
        {
            Subject = " Subject ",
            Reference = " Ref ",
            CommentText = "<p>Hi</p><script>alert(1)</script>",
            ParentId = parentId
        };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("saved", redirect.RouteValues!["result"]);
        var added = api.AddedReviewComment!.Value;
        Assert.Equal(parentId, added.ParentId);
        Assert.Equal("Subject", added.Subject);
        Assert.Equal("Ref", added.Reference);
        Assert.Equal("<p>Hi</p>", added.Text);
    }

    [Fact]
    public async Task OnPostSaveAsync_UpdatesTheComment_WhenEditing()
    {
        var (pageModel, api) = CreatePage(DraftProfile());
        var commentId = Guid.NewGuid();
        pageModel.Input = new ReviewCommentInput
        {
            Subject = "S",
            Reference = "R",
            CommentText = "Body",
            CommentId = commentId,
            LastUpdated = Convert.ToBase64String(RowVersion)
        };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        var updated = api.UpdatedReviewComment!.Value;
        Assert.Equal(commentId, updated.CommentId);
        Assert.Equal(RowVersion, updated.LastUpdated);
        Assert.Null(api.AddedReviewComment);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReportsAFailure_WhenTheRowVersionIsNotValid()
    {
        var (pageModel, api) = CreatePage(DraftProfile());
        pageModel.Input = new ReviewCommentInput { Subject = "S", Reference = "R", CommentText = "Body", CommentId = Guid.NewGuid(), LastUpdated = "not base64" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.StartsWith("Review comment save failed: ", pageModel.ActionMessage, StringComparison.Ordinal);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.Null(api.UpdatedReviewComment);
    }

    [Theory]
    [InlineData(ReviewCommentOutcome.Conflict, "The review comment has been edited by another user.")]
    [InlineData(ReviewCommentOutcome.Forbidden, "Not yours.")]
    public async Task OnPostSaveAsync_ReportsTheFailure_AndKeepsThePanelOpen(ReviewCommentOutcome outcome, string message)
    {
        var (pageModel, _) = CreatePage(DraftProfile(), reviewCommentResult: new ReviewCommentResult { Outcome = outcome, ErrorMessage = message });
        pageModel.Input = new ReviewCommentInput { Subject = "S", Reference = "R", CommentText = "Body" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal($"Review comment save failed: {message}", pageModel.ActionMessage);
        Assert.Equal(ReviewCommentPanel.Add, pageModel.Panel);
        Assert.Equal("S", pageModel.Input.Subject);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReportsAFailure_WhenTheApiCallThrows()
    {
        var (pageModel, _) = CreatePage(DraftProfile(), throwOnChangeReviewComment: new HttpRequestException("boom"));
        pageModel.Input = new ReviewCommentInput { Subject = "S", Reference = "R", CommentText = "Body" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ActionMessageIsError);
    }

    [Fact]
    public async Task OnPostSaveAsync_DoesNotCallTheApi_WhenTheUserCannotEdit()
    {
        var (pageModel, api) = CreatePage(DraftProfile() with { CanEditProfileSection = false });
        pageModel.Input = new ReviewCommentInput { Subject = "S", Reference = "R", CommentText = "Body" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.Null(api.AddedReviewComment);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsNotFound_WhenTheUserCannotViewReviewComments()
    {
        var (pageModel, _) = CreatePage(Profile());

        Assert.IsType<NotFoundResult>(await pageModel.OnPostSaveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnPostDeleteAsync_DeletesTheComment_AndRedirectsWithADeletedNotice()
    {
        var comment = Comment(canEdit: true);
        var (pageModel, api) = CreatePage(DraftProfile(), [comment]);

        var result = await pageModel.OnPostDeleteAsync(comment.Id, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("deleted", redirect.RouteValues!["result"]);
        Assert.Equal(comment.Id, api.DeletedReviewCommentId);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReportsTheFailure_AndReopensTheComment()
    {
        var comment = Comment(canEdit: true);
        var (pageModel, _) = CreatePage(
            DraftProfile(),
            [comment],
            reviewCommentResult: new ReviewCommentResult { Outcome = ReviewCommentOutcome.Forbidden, ErrorMessage = "It has replies." });

        var result = await pageModel.OnPostDeleteAsync(comment.Id, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Review comment deletion failed: It has replies.", pageModel.ActionMessage);
        Assert.Equal(ReviewCommentPanel.View, pageModel.Panel);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReportsAFailure_WhenTheApiCallThrows()
    {
        var (pageModel, _) = CreatePage(DraftProfile(), throwOnChangeReviewComment: new TaskCanceledException());

        var result = await pageModel.OnPostDeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.StartsWith("Review comment deletion failed: ", pageModel.ActionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnPostDeleteAsync_DoesNotCallTheApi_WhenTheUserCannotEdit()
    {
        var (pageModel, api) = CreatePage(DraftProfile() with { CanEditProfileSection = false });

        var result = await pageModel.OnPostDeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(api.DeletedReviewCommentId);
    }

    [Fact]
    public async Task BuildUrls_CarryTheModeCommentAndPaging()
    {
        var (pageModel, _) = CreatePage(DraftProfile());
        await pageModel.OnGetAsync(CancellationToken.None);
        var commentId = Guid.NewGuid();

        Assert.Contains("mode=add", pageModel.BuildAddUrl(), StringComparison.Ordinal);
        Assert.Contains("mode=reply", pageModel.BuildReplyUrl(commentId), StringComparison.Ordinal);
        Assert.Contains("mode=edit", pageModel.BuildEditUrl(commentId), StringComparison.Ordinal);
        Assert.Contains($"commentId={commentId}", pageModel.BuildViewUrl(commentId), StringComparison.Ordinal);
        Assert.Contains("pageNumber=2", pageModel.BuildListUrl(2), StringComparison.Ordinal);
    }
}
