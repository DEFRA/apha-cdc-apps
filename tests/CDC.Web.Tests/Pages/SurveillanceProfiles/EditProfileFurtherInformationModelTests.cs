using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditProfileFurtherInformationModelTests
{
    private const string FurtherInformationPage = "/SurveillanceProfiles/EditProfileFurtherInformation";

    private static readonly Guid NoteTypeId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly Guid ThirdQuestionId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly string RowVersionText = Convert.ToBase64String(RowVersion);

    // Which calls to CDC.Api the fake should fail, so the page's resilience can be tested.
    private sealed record Failures(
        Exception? NoteTypes = null,
        Exception? SectionNotes = null,
        Exception? MasterList = null,
        Exception? Questions = null,
        Exception? Change = null);

    private static ProfileNoteTypeDto FurtherInformationType() =>
        new() { Id = NoteTypeId, Name = "Source of further information", PluralName = "Sources of further information" };

    private static ProfileQuestionMetadataDto QuestionMetadata(Guid id, int number) =>
        new() { Id = id, SectionId = SummarySectionId, ShortName = $"Question {number}", QuestionNumber = number, Fields = [] };

    // The Summary section holds three questions (1.1 to 1.3) so that a note can be linked to several of them.
    private static ProfileQuestionnaireMetadataDto Metadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SummarySectionId,
                Name = "Summary",
                ShortName = "SUMMARY",
                SectionNumber = 1,
                Questions = [QuestionMetadata(QuestionId, 1), QuestionMetadata(SecondQuestionId, 2), QuestionMetadata(ThirdQuestionId, 3)]
            },
            new ProfileSectionMetadataDto { Id = EpidemiologySectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 2, Questions = [] }
        ]
    };

    private static IReadOnlyList<ProfileQuestionInfoDto> Questions() =>
    [
        new() { Id = QuestionId, Name = "Is it endemic?", QuestionNumber = 1 },
        new() { Id = SecondQuestionId, Name = "How is it spread?", QuestionNumber = 2 },
        new() { Id = ThirdQuestionId, Name = "Is it notifiable?", QuestionNumber = 3 }
    ];

    private static ProfileNoteDto Note(string text, params QuestionReferenceDto[] references) =>
        new() { Id = Guid.NewGuid(), NoteText = text, LastUpdated = RowVersion, QuestionReferences = references };

    private static QuestionReferenceDto Reference(Guid questionId, Guid? sectionId = null) =>
        new() { ProfileSectionId = sectionId ?? SummarySectionId, ProfileQuestionId = questionId };

    /// <summary>A note linked to the given questions of the Summary section.</summary>
    private static ProfileNoteDto Linked(string text, params Guid[] questionIds) =>
        Note(text, [.. questionIds.Select(questionId => Reference(questionId))]);

    /// <summary>A note that is in the version's master list but linked to no question of the Summary section.</summary>
    private static ProfileNoteDto Unlinked(string text) => Note(text, Reference(Guid.NewGuid(), EpidemiologySectionId));

    private static (EditProfileFurtherInformationModel PageModel, FakeProfileSectionsApiService Api, AlwaysEnabledLogger<EditProfileFurtherInformationModel> Log) CreatePage(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileNoteDto>? sectionNotes = null,
        IReadOnlyList<ProfileNoteDto>? masterList = null,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        ProfileNoteChangeResult? changeResult = null,
        Failures? failures = null)
    {
        var log = new AlwaysEnabledLogger<EditProfileFurtherInformationModel>();
        var failing = failures ?? new Failures();
        var inSection = sectionNotes ?? [];
        var api = new FakeProfileSectionsApiService(
            metadata: Metadata(),
            noteTypes: noteTypes ?? [FurtherInformationType()],
            notesByNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [NoteTypeId] = inSection },
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [NoteTypeId] = masterList ?? inSection },
            throwOnGetProfileNoteTypes: failing.NoteTypes,
            throwOnGetProfileNotesBySection: failing.SectionNotes,
            throwOnGetProfileNotesByVersion: failing.MasterList,
            questionInfos: Questions(),
            throwOnGetProfileQuestionInfoList: failing.Questions,
            noteChangeResult: changeResult,
            throwOnUpdateProfileNotes: failing.Change);

        var pageModel = Configure(new EditProfileFurtherInformationModel(new FakeApiClient(manageProfile: manageProfile), api, log));

        return (pageModel, api, log);
    }

    private static ManageProfileViewModel ReadOnlyProfile() => DraftProfile() with { CanEditProfileSection = false };

    [Fact]
    public void ActiveTab_IsFurtherInformation() =>
        Assert.Equal(ProfileSectionTab.FurtherInformation, CreatePage(Profile()).PageModel.ActiveTab);

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var (pageModel, _, _) = CreatePage(manageProfile: null);

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_ListsTheSectionsSources_SortedByTextWithTheirQuestionReferences()
    {
        var (pageModel, _, _) = CreatePage(
            DraftProfile(),
            [Linked("<p>Zebra source</p>", QuestionId, SecondQuestionId), Linked("avian <b>source</b>", SecondQuestionId)]);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["avian source", "Zebra source"], pageModel.Rows.Select(row => row.PlainText));
        Assert.Equal(["1.2", "1.1, 1.2"], pageModel.Rows.Select(row => row.QuestionReferenceDisplay));
        Assert.Equal("<p>Zebra source</p>", pageModel.Rows[1].TextHtml);
        Assert.Equal(RowVersionText, pageModel.Rows[0].LastUpdated);
        Assert.Equal(2, pageModel.TotalRecords);
        Assert.Equal(1, pageModel.TotalPages);
    }

    [Theory]
    [InlineData("Reference", "Ascending", "Alpha,Mid,Zebra")]
    [InlineData("Reference", "Descending", "Zebra,Mid,Alpha")]
    [InlineData("QuestionReference", "Ascending", "Mid,Zebra,Alpha")]
    [InlineData("QuestionReference", "Descending", "Alpha,Zebra,Mid")]
    [InlineData("questionreference", "descending", "Alpha,Zebra,Mid")]
    public async Task OnGetAsync_SortsByTheChosenColumnAndDirection(string column, string direction, string expectedOrder)
    {
        var (pageModel, _, _) = CreatePage(
            DraftProfile(),
            [Linked("Alpha", ThirdQuestionId), Linked("Mid", QuestionId), Linked("Zebra", SecondQuestionId)]);
        pageModel.SortColumn = column;
        pageModel.SortDirection = direction;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedOrder, string.Join(",", pageModel.Rows.Select(row => row.PlainText)));
    }

    [Theory]
    [InlineData("questionreference", "descending", "QuestionReference", "Descending")]
    [InlineData("Bogus", "Sideways", "Reference", "Ascending")]
    public async Task OnGetAsync_NormalisesTheSortToAKnownColumnAndDirection(string column, string direction, string expectedColumn, string expectedDirection)
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);
        pageModel.SortColumn = column;
        pageModel.SortDirection = direction;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedColumn, pageModel.SortColumn);
        Assert.Equal(expectedDirection, pageModel.SortDirection);
    }

    [Fact]
    public async Task OnGetAsync_PagesTheSources_AndClampsAnOutOfRangePage()
    {
        var notes = Enumerable.Range(1, 12).Select(index => Linked($"Note {index:00}", QuestionId)).ToList();
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);
        pageModel.PageNumber = 99;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(12, pageModel.TotalRecords);
        Assert.Equal(2, pageModel.TotalPages);
        Assert.Equal(2, pageModel.PageNumber);
        Assert.Equal(2, pageModel.ListPageNumber);
        Assert.Equal(2, pageModel.ListTotalPages);
        Assert.Equal(["Note 11", "Note 12"], pageModel.Rows.Select(row => row.PlainText));
    }

    [Fact]
    public async Task OnGetAsync_ClampsAPageNumberBelowOne()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);
        pageModel.PageNumber = -3;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, pageModel.PageNumber);
    }

    [Theory]
    [InlineData("25", "25", 12)]
    [InlineData("abc", "10", 10)]
    [InlineData("7", "10", 10)]
    public async Task OnGetAsync_UsesTheChosenPageSize_AndFallsBackToTheFirstOptionForAnUnknownOne(string requested, string expectedSize, int expectedRows)
    {
        var notes = Enumerable.Range(1, 12).Select(index => Linked($"Note {index:00}", QuestionId)).ToList();
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);
        pageModel.PageSize = requested;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedSize, pageModel.PageSize);
        Assert.Equal(expectedRows, pageModel.Rows.Count);
    }

    [Fact]
    public async Task OnGetAsync_OffersEditing_ButNotSelect_WhenEverySourceIsAlreadyLinkedToTheSection()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.CanEdit);
        Assert.False(pageModel.CanSelect);
        Assert.Empty(pageModel.AvailableNoteChoices);
    }

    [Fact]
    public async Task OnGetAsync_OffersSelect_ForTheMasterListSourcesNotYetLinkedToTheSection()
    {
        var inSection = Linked("In the section", QuestionId);
        var available = Unlinked("<p>" + new string('x', 60) + "</p>");
        var (pageModel, _, _) = CreatePage(DraftProfile(), [inSection], masterList: [inSection, available]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.CanSelect);
        var choice = Assert.Single(pageModel.AvailableNoteChoices);
        Assert.Equal(available.Id, choice.Id);
        Assert.Equal(new string('x', 47) + "...", choice.Text);
        Assert.False(choice.IsSelected);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheTableButNoEditing_WhenTheUserCannotEdit()
    {
        var (pageModel, api, _) = CreatePage(ReadOnlyProfile(), [Linked("Source", QuestionId)], masterList: [Linked("Source", QuestionId), Unlinked("Other")]);
        pageModel.Mode = "add";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Single(pageModel.Rows);
        Assert.False(pageModel.CanEdit);
        Assert.False(pageModel.CanSelect);
        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
        Assert.Null(api.RequestedQuestionInfoSectionId);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheLegacyEmptyMessageAndLabels_WhenTheSectionHasNoSources()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.Rows);
        Assert.Equal("There are no sources of further information to display.", pageModel.NoNotesMessage);
        Assert.Equal("Add a new source of further information:", pageModel.AddLabel);
        Assert.Equal("Select a source of further information from the master list:", pageModel.SelectLabel);
        Assert.Equal("Are you sure you want to delete the association to this source of further information?", pageModel.DeleteConfirmation);
        Assert.True(pageModel.CanEdit);
    }

    [Fact]
    public async Task OnGetAsync_MatchesTheNoteTypeByName_RegardlessOfSpacing()
    {
        var noteTypes = new List<ProfileNoteTypeDto> { new() { Id = NoteTypeId, Name = "SourceOfFurtherInformation", PluralName = "Sources of further information" } };
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)], noteTypes: noteTypes);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Single(pageModel.Rows);
        Assert.Equal("SourceOfFurtherInformation", pageModel.NoteTypeName);
    }

    [Fact]
    public async Task OnGetAsync_UsesTheFallbackNames_WhenTheNoteTypeHasNone()
    {
        var noteTypes = new List<ProfileNoteTypeDto> { new() { Id = NoteTypeId, Name = "SourceOfFurtherInformation" } };
        var (pageModel, _, _) = CreatePage(DraftProfile(), noteTypes: noteTypes);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("SourceOfFurtherInformation", pageModel.NoteTypeName);
        Assert.Equal("Sources of further information", pageModel.NoteTypePluralName);
    }

    [Fact]
    public async Task OnGetAsync_SwitchesEditingOff_WhenTheNoteTypeIsNotFound()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), noteTypes: []);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.CanEdit);
        Assert.Empty(pageModel.Rows);
        Assert.Null(pageModel.ActionMessage);
        Assert.Equal("Source of further information", pageModel.NoteTypeName);
        Assert.Equal("There are no sources of further information to display.", pageModel.NoNotesMessage);
    }

    [Theory]
    [InlineData("note types")]
    [InlineData("section notes")]
    [InlineData("master list")]
    public async Task OnGetAsync_ReportsTheFailureAndSwitchesEditingOff_WhenLoadingFails(string failingCall)
    {
        var failure = new HttpRequestException("boom");
        var (pageModel, _, log) = CreatePage(
            DraftProfile(),
            [Linked("Source", QuestionId)],
            failures: new Failures(
                NoteTypes: failingCall == "note types" ? failure : null,
                SectionNotes: failingCall == "section notes" ? failure : null,
                MasterList: failingCall == "master list" ? failure : null));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Equal("We could not load the sources of further information. Try again later.", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.True(pageModel.HasFormErrors);
        Assert.False(pageModel.CanEdit);
        Assert.Empty(pageModel.Rows);
        Assert.Contains(log.Messages, message => message.StartsWith("Failed to load profile", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_TreatsATimeoutLikeAnyOtherLoadFailure()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), failures: new Failures(SectionNotes: new TaskCanceledException()));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ActionMessageIsError);
    }

    [Fact]
    public async Task OnGetAsync_LoadsNothing_WhenTheProfileHasNoVersion()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile() with { CurrentProfileVersionId = Guid.Empty }, [Linked("Source", QuestionId)]);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasNoVersion);
        Assert.Empty(pageModel.Rows);
        Assert.False(pageModel.CanEdit);
        Assert.Null(api.RequestedQuestionInfoSectionId);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("ADD")]
    public async Task OnGetAsync_OpensTheAddPanel_WithEveryQuestionOfTheSectionUnticked(string mode)
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Mode = mode;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.Add, pageModel.Panel);
        Assert.Equal("Add a source of further information", pageModel.PanelHeading);
        Assert.Equal("add", pageModel.Input.Mode);
        Assert.Equal(["1.1", "1.2", "1.3"], pageModel.QuestionChoices.Select(choice => choice.Number));
        Assert.Equal(["Is it endemic?", "How is it spread?", "Is it notifiable?"], pageModel.QuestionChoices.Select(choice => choice.Name));
        Assert.DoesNotContain(pageModel.QuestionChoices, choice => choice.IsTicked);
        Assert.Null(pageModel.PanelNoteHtml);
        Assert.Equal(SummarySectionId, api.RequestedQuestionInfoSectionId);
    }

    [Fact]
    public async Task OnGetAsync_OpensTheSelectPanel_WithNothingChosen()
    {
        var available = Unlinked("<p>Master only</p>");
        var (pageModel, _, _) = CreatePage(DraftProfile(), masterList: [available]);
        pageModel.Mode = "select";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.Select, pageModel.Panel);
        Assert.Equal("Select a source of further information", pageModel.PanelHeading);
        Assert.Null(pageModel.PanelNoteHtml);
        Assert.Null(pageModel.Input.NoteId);
        Assert.DoesNotContain(pageModel.AvailableNoteChoices, choice => choice.IsSelected);
    }

    [Fact]
    public async Task OnGetAsync_OpensTheSelectPanel_ShowingTheChosenSourceWithoutParagraphTags()
    {
        var available = Unlinked("<p>Master only <i>source</i></p>");
        var (pageModel, _, _) = CreatePage(DraftProfile(), masterList: [Unlinked("Another"), available]);
        pageModel.Mode = "select";
        pageModel.NoteId = available.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Master only <i>source</i>", pageModel.PanelNoteHtml);
        Assert.Equal(available.Id, pageModel.Input.NoteId);
        Assert.Equal(RowVersionText, pageModel.Input.LastUpdated);
        Assert.Equal(available.Id, Assert.Single(pageModel.AvailableNoteChoices, choice => choice.IsSelected).Id);
        Assert.DoesNotContain(pageModel.QuestionChoices, choice => choice.IsTicked);
    }

    [Fact]
    public async Task OnGetAsync_DoesNotOpenTheSelectPanel_WhenTheMasterListHoldsNothingToSelect()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);
        pageModel.Mode = "select";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
    }

    [Fact]
    public async Task OnGetAsync_OpensTheViewPanel_WithTheQuestionsTheSourceIsLinkedToTicked()
    {
        var source = Note("<p>A source</p>", Reference(QuestionId), Reference(SecondQuestionId), Reference(Guid.NewGuid(), EpidemiologySectionId));
        var (pageModel, _, _) = CreatePage(DraftProfile(), [source]);
        pageModel.NoteId = source.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.View, pageModel.Panel);
        Assert.Equal("View a source of further information", pageModel.PanelHeading);
        Assert.Equal("view", pageModel.Input.Mode);
        Assert.Equal(source.Id, pageModel.Input.NoteId);
        Assert.Equal("A source", pageModel.PanelNoteHtml);
        Assert.Equal([true, true, false], pageModel.QuestionChoices.Select(choice => choice.IsTicked));
    }

    [Fact]
    public async Task OnGetAsync_OpensNoPanel_WhenTheViewedSourceIsNotInTheSection()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);
        pageModel.NoteId = Guid.NewGuid();

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
        Assert.Empty(pageModel.QuestionChoices);
    }

    [Fact]
    public async Task PanelHeading_IsEmpty_WhenNoPanelIsOpen()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(string.Empty, pageModel.PanelHeading);
    }

    [Fact]
    public async Task OnGetAsync_OpensNoPanelAndReportsIt_WhenTheQuestionsCannotBeLoaded()
    {
        var (pageModel, _, log) = CreatePage(DraftProfile(), failures: new Failures(Questions: new HttpRequestException("boom")));
        pageModel.Mode = "add";

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
        Assert.Equal("We could not load the questions for this section. Try again later.", pageModel.ActionMessage);
        Assert.True(pageModel.HasFormErrors);
        Assert.Contains(log.Messages, message => message.Contains("questions from CDC.Api", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("saved", "Source of further information save succeeded")]
    [InlineData("deleted", "Source of further information deletion succeeded")]
    [InlineData("SAVED", "Source of further information save succeeded")]
    public async Task OnGetAsync_ShowsTheResultOfThePreviousChange(string notice, string expectedMessage)
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.Notice = notice;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedMessage, pageModel.ActionMessage);
        Assert.False(pageModel.ActionMessageIsError);
        Assert.False(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task OnGetAsync_IgnoresAnUnknownResultNotice()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.Notice = "<script>";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Null(pageModel.ActionMessage);
    }

    [Fact]
    public async Task OnGetAsync_KeepsALoadFailureMessage_WhenAResultNoticeIsPresent()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), failures: new Failures(SectionNotes: new HttpRequestException("boom")));
        pageModel.Notice = "saved";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.ActionMessageIsError);
        Assert.StartsWith("We could not load", pageModel.ActionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HasFormErrors_IsFalse_UntilAValidationErrorOrFailureIsReported()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());

        Assert.False(pageModel.HasFormErrors);

        pageModel.Input = new ProfileNoteInput { Mode = "add" };
        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.True(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var (pageModel, _, _) = CreatePage(manageProfile: null);

        Assert.IsType<NotFoundResult>(await pageModel.OnPostSaveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsThePage_WhenTheProfileHasNoVersion()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile() with { CurrentProfileVersionId = Guid.Empty });
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "Text", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    public async Task OnPostSaveAsync_ReturnsBadRequest_ForAnUnknownPanel(string? mode)
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = new ProfileNoteInput { Mode = mode, NoteText = "Text", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_Add_ReportsEveryMissingField_AndKeepsThePanelOpen()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "<p>&nbsp;</p>" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            [
                new ProfileNoteFormError("Input_NoteText", "Enter a source of further information"),
                new ProfileNoteFormError("Input_QuestionIds_0", "Select at least one question")
            ],
            pageModel.FormErrors);
        Assert.Equal(ProfileNotePanel.Add, pageModel.Panel);
        Assert.Equal(3, pageModel.QuestionChoices.Count);
        Assert.True(pageModel.HasFormErrors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_Add_InsertsTheSanitisedNote_LinkedToTheTickedQuestions_AndRedirects()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = new ProfileNoteInput
        {
            Mode = "add",
            NoteText = "<p>Hi <a href=\"https://example.com\">link</a></p><script>alert(1)</script>",
            QuestionIds = [QuestionId, SecondQuestionId]
        };
        pageModel.PageSize = "25";
        pageModel.SortColumn = "QuestionReference";
        pageModel.SortDirection = "Descending";

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(FurtherInformationPage, redirect.PageName);
        Assert.Equal("saved", redirect.RouteValues!["result"]);
        Assert.Equal(ProfileId, redirect.RouteValues["profileId"]);
        Assert.Equal(SummarySectionId, redirect.RouteValues["section"]);
        Assert.Equal("25", redirect.RouteValues["pageSize"]);
        Assert.Equal("QuestionReference", redirect.RouteValues["sortColumn"]);
        Assert.Equal("Descending", redirect.RouteValues["sortDirection"]);
        Assert.Equal(ProfileVersionId, api.ChangedNotesProfileVersionId);
        Assert.Equal(NoteTypeId, api.ChangedNotesNoteTypeId);
        Assert.Empty(api.SavedNoteUpdates!);
        var insert = Assert.Single(api.SavedNoteInserts!);
        Assert.NotEqual(Guid.Empty, insert.Id);
        Assert.Equal("<p>Hi <a href=\"https://example.com\">link</a></p>", insert.NoteText);
        Assert.Equal([QuestionId, SecondQuestionId], insert.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        Assert.All(insert.QuestionReferenceAdds, reference => Assert.Equal(SummarySectionId, reference.ProfileSectionId));
    }

    [Fact]
    public async Task OnPostSaveAsync_Add_IgnoresTicksThatAreNotQuestionsOfTheSection()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = new ProfileNoteInput
        {
            Mode = "add",
            NoteText = "Text",
            QuestionIds = [Guid.NewGuid(), QuestionId, QuestionId]
        };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        var insert = Assert.Single(api.SavedNoteInserts!);
        Assert.Equal([QuestionId], insert.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
    }

    [Fact]
    public async Task OnPostSaveAsync_Add_RejectsASubmissionWhoseOnlyTicksAreNotQuestionsOfTheSection()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "Text", QuestionIds = [Guid.NewGuid()] };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([new ProfileNoteFormError("Input_QuestionIds_0", "Select at least one question")], pageModel.FormErrors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_Select_RequiresASourceToBeChosen()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), masterList: [Unlinked("Master only")]);
        pageModel.Input = new ProfileNoteInput { Mode = "select", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal([new ProfileNoteFormError("NoteId", "Select a source of further information")], pageModel.FormErrors);
        Assert.Equal(ProfileNotePanel.Select, pageModel.Panel);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_Select_RequiresAQuestionToBeTicked()
    {
        var available = Unlinked("Master only");
        var (pageModel, api, _) = CreatePage(DraftProfile(), masterList: [available]);
        pageModel.Input = new ProfileNoteInput { Mode = "select", NoteId = available.Id, LastUpdated = RowVersionText };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([new ProfileNoteFormError("Input_QuestionIds_0", "Select at least one question")], pageModel.FormErrors);
        Assert.Equal(available.Id, pageModel.Input.NoteId);
        Assert.NotNull(pageModel.PanelNoteHtml);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_Select_LinksTheChosenSourceToTheTickedQuestions()
    {
        var available = Unlinked("<p>Master only</p>");
        var (pageModel, api, _) = CreatePage(DraftProfile(), masterList: [available]);
        pageModel.Input = new ProfileNoteInput
        {
            Mode = "select",
            NoteId = available.Id,
            LastUpdated = RowVersionText,
            NoteText = "<script>ignored</script>",
            QuestionIds = [QuestionId, SecondQuestionId]
        };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("saved", redirect.RouteValues!["result"]);
        Assert.Empty(api.SavedNoteInserts!);
        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.Equal(available.Id, update.Id);
        Assert.Equal("<p>Master only</p>", update.NoteText);
        Assert.Equal(RowVersion, update.LastUpdated);
        Assert.Equal([QuestionId, SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        Assert.Empty(update.QuestionReferenceRemoves);
    }

    [Fact]
    public async Task OnPostSaveAsync_View_AddsAndRemovesLinksSoTheyMatchTheTicks()
    {
        var source = Linked("<p>Linked source</p>", QuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [source]);
        pageModel.Input = new ProfileNoteInput { Mode = "view", NoteId = source.Id, LastUpdated = RowVersionText, QuestionIds = [SecondQuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.Equal([SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        Assert.Equal([QuestionId], update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId));
    }

    [Fact]
    public async Task OnPostSaveAsync_View_LeavesLinksInOtherSectionsAlone()
    {
        var elsewhere = Reference(Guid.NewGuid(), EpidemiologySectionId);
        var source = Note("Source", Reference(QuestionId), elsewhere);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [source]);
        pageModel.Input = new ProfileNoteInput { Mode = "view", NoteId = source.Id, LastUpdated = RowVersionText, QuestionIds = [SecondQuestionId] };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.DoesNotContain(update.QuestionReferenceRemoves, reference => reference.ProfileSectionId == elsewhere.ProfileSectionId);
    }

    [Fact]
    public async Task OnPostSaveAsync_View_DoesNotCallTheApi_WhenTheTicksAlreadyMatchTheLinks()
    {
        var source = Linked("Source", QuestionId, SecondQuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [source]);
        pageModel.Input = new ProfileNoteInput { Mode = "view", NoteId = source.Id, LastUpdated = RowVersionText, QuestionIds = [SecondQuestionId, QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("saved", redirect.RouteValues!["result"]);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_View_RequiresAQuestionToBeTicked()
    {
        var source = Linked("Source", QuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [source]);
        pageModel.Input = new ProfileNoteInput { Mode = "view", NoteId = source.Id, LastUpdated = RowVersionText };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([new ProfileNoteFormError("Input_QuestionIds_0", "Select at least one question")], pageModel.FormErrors);
        Assert.Equal(ProfileNotePanel.View, pageModel.Panel);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_View_ReportsTheSourceNoLongerExisting_AndClosesThePanel()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);
        pageModel.Input = new ProfileNoteInput { Mode = "view", NoteId = Guid.NewGuid(), LastUpdated = RowVersionText, QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Source of further information save failed: The source of further information no longer exists.", pageModel.ActionMessage);
        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("view")]
    public async Task OnPostSaveAsync_ReportsAFailure_WhenTheRowVersionIsNotValid(string mode)
    {
        // The Select panel only offers sources not yet linked to the section; the View panel only those that are.
        var source = mode == "select" ? Unlinked("Source") : Linked("Source", QuestionId);
        var (pageModel, api, log) = mode == "select"
            ? CreatePage(DraftProfile(), masterList: [source])
            : CreatePage(DraftProfile(), [source]);
        pageModel.Input = new ProfileNoteInput { Mode = mode, NoteId = source.Id, LastUpdated = "not base64", QuestionIds = [SecondQuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            "Source of further information save failed: The source of further information could not be identified. Reopen it and try again.",
            pageModel.ActionMessage);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
        Assert.Contains(log.Messages, message => message.Contains("refused", StringComparison.Ordinal) && message.Contains("ValidationFailed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ProfileNoteChangeOutcome.Conflict, "Another user changed it.")]
    [InlineData(ProfileNoteChangeOutcome.ValidationFailed, "Not accepted.")]
    [InlineData(ProfileNoteChangeOutcome.Error, "We could not save the change. Try again later.")]
    public async Task OnPostSaveAsync_ReportsTheFailure_AndKeepsThePanelOpenWithWhatWasEntered(ProfileNoteChangeOutcome outcome, string message)
    {
        var (pageModel, _, log) = CreatePage(DraftProfile(), changeResult: new ProfileNoteChangeResult { Outcome = outcome, ErrorMessage = message });
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "<p>Typed text</p>", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal($"Source of further information save failed: {message}", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.True(pageModel.HasFormErrors);
        Assert.Equal(ProfileNotePanel.Add, pageModel.Panel);
        Assert.Equal("<p>Typed text</p>", pageModel.Input.NoteText);
        Assert.True(Assert.Single(pageModel.QuestionChoices, choice => choice.Id == QuestionId).IsTicked);
        Assert.Equal(outcome != ProfileNoteChangeOutcome.Error, log.Messages.Any(entry => entry.Contains("was refused by CDC.Api", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnPostSaveAsync_ReportsAFailure_WhenTheApiCallThrows(Type exceptionType)
    {
        var failure = (Exception)Activator.CreateInstance(exceptionType)!;
        var (pageModel, _, log) = CreatePage(DraftProfile(), failures: new Failures(Change: failure));
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "Text", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Source of further information save failed: We could not save the change. Try again later.", pageModel.ActionMessage);
        Assert.Contains(log.Messages, message => message.StartsWith("Failed to change a source of further information", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Messages, message => message.Contains("was refused by CDC.Api", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnPostSaveAsync_DoesNotCallTheApi_WhenTheUserCannotEdit()
    {
        var (pageModel, api, _) = CreatePage(ReadOnlyProfile());
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "Text", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            "Source of further information save failed: You cannot change a source of further information on this version.",
            pageModel.ActionMessage);
        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_KeepsTheLoadFailureMessage_WhenEditingIsOffBecauseLoadingFailed()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), failures: new Failures(SectionNotes: new HttpRequestException("boom")));
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "Text", QuestionIds = [QuestionId] };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.StartsWith("We could not load", pageModel.ActionMessage, StringComparison.Ordinal);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsThePageWithoutSaving_WhenTheQuestionsCannotBeLoaded()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), failures: new Failures(Questions: new HttpRequestException("boom")));
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "Text", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("We could not load the questions for this section. Try again later.", pageModel.ActionMessage);
        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var (pageModel, _, _) = CreatePage(manageProfile: null);

        Assert.IsType<NotFoundResult>(await pageModel.OnPostDeleteAsync(Guid.NewGuid(), RowVersionText, CancellationToken.None));
    }

    [Fact]
    public async Task OnPostDeleteAsync_UnlinksTheSourceFromTheSectionsQuestionsOnly_AndRedirects()
    {
        var elsewhere = Reference(Guid.NewGuid(), EpidemiologySectionId);
        var source = Note("Source", Reference(QuestionId), Reference(SecondQuestionId), elsewhere);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [source]);

        var result = await pageModel.OnPostDeleteAsync(source.Id, RowVersionText, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("deleted", redirect.RouteValues!["result"]);
        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.Equal(source.Id, update.Id);
        Assert.Equal(RowVersion, update.LastUpdated);
        Assert.Empty(update.QuestionReferenceAdds);
        Assert.Equal(
            new[] { QuestionId, SecondQuestionId }.Order(),
            update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId).Order());
        Assert.All(update.QuestionReferenceRemoves, reference => Assert.Equal(SummarySectionId, reference.ProfileSectionId));
        Assert.Empty(api.SavedNoteInserts!);
    }

    [Fact]
    public async Task OnPostDeleteAsync_RedirectsWithoutCallingTheApi_WhenTheSourceHasNoLinksInTheSection()
    {
        var source = Unlinked("Source");
        var (pageModel, api, _) = CreatePage(DraftProfile(), [source]);

        var result = await pageModel.OnPostDeleteAsync(source.Id, RowVersionText, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReportsTheSourceNoLongerExisting()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);

        var result = await pageModel.OnPostDeleteAsync(Guid.NewGuid(), RowVersionText, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Source of further information deletion failed: The source of further information no longer exists.", pageModel.ActionMessage);
        Assert.True(pageModel.ActionMessageIsError);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64")]
    [InlineData("AQID")]
    public async Task OnPostDeleteAsync_ReportsARowVersionThatCannotBeIdentified(string? lastUpdated)
    {
        var source = Linked("Source", QuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [source]);

        var result = await pageModel.OnPostDeleteAsync(source.Id, lastUpdated, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            "Source of further information deletion failed: The source of further information could not be identified. Reload the page and try again.",
            pageModel.ActionMessage);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData(ProfileNoteChangeOutcome.Conflict, "Another user changed it.")]
    [InlineData(ProfileNoteChangeOutcome.Error, "We could not save the change. Try again later.")]
    public async Task OnPostDeleteAsync_ReportsTheFailure(ProfileNoteChangeOutcome outcome, string message)
    {
        var source = Linked("Source", QuestionId);
        var (pageModel, _, _) = CreatePage(DraftProfile(), [source], changeResult: new ProfileNoteChangeResult { Outcome = outcome, ErrorMessage = message });

        var result = await pageModel.OnPostDeleteAsync(source.Id, RowVersionText, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal($"Source of further information deletion failed: {message}", pageModel.ActionMessage);
        Assert.Single(pageModel.Rows);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReportsAFailure_WhenTheApiCallThrows()
    {
        var source = Linked("Source", QuestionId);
        var (pageModel, _, _) = CreatePage(DraftProfile(), [source], failures: new Failures(Change: new TaskCanceledException()));

        var result = await pageModel.OnPostDeleteAsync(source.Id, RowVersionText, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.StartsWith("Source of further information deletion failed: ", pageModel.ActionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnPostDeleteAsync_DoesNotCallTheApi_WhenTheUserCannotEdit()
    {
        var source = Linked("Source", QuestionId);
        var (pageModel, api, _) = CreatePage(ReadOnlyProfile(), [source]);

        var result = await pageModel.OnPostDeleteAsync(source.Id, RowVersionText, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            "Source of further information deletion failed: You cannot change a source of further information on this version.",
            pageModel.ActionMessage);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReturnsThePage_WhenTheProfileHasNoVersion()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile() with { CurrentProfileVersionId = Guid.Empty });

        var result = await pageModel.OnPostDeleteAsync(Guid.NewGuid(), RowVersionText, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostTickAllAsync_TicksEveryQuestion_AndKeepsWhatWasEntered()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "<p>keep me</p>", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostTickAllAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ProfileNotePanel.Add, pageModel.Panel);
        Assert.DoesNotContain(pageModel.QuestionChoices, choice => !choice.IsTicked);
        Assert.Equal(3, pageModel.Input.QuestionIds.Count);
        Assert.Equal("<p>keep me</p>", pageModel.Input.NoteText);
        Assert.Empty(pageModel.FormErrors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostUntickAllAsync_ClearsEveryTick_AndKeepsTheChosenSourceShown()
    {
        var available = Unlinked("<p>Master only</p>");
        var (pageModel, _, _) = CreatePage(DraftProfile(), masterList: [available]);
        pageModel.Input = new ProfileNoteInput { Mode = "select", NoteId = available.Id, LastUpdated = RowVersionText, QuestionIds = [QuestionId, SecondQuestionId] };

        var result = await pageModel.OnPostUntickAllAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ProfileNotePanel.Select, pageModel.Panel);
        Assert.DoesNotContain(pageModel.QuestionChoices, choice => choice.IsTicked);
        Assert.Empty(pageModel.Input.QuestionIds);
        Assert.Equal("Master only", pageModel.PanelNoteHtml);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("bogus")]
    public async Task TickAndUntickAll_ReturnBadRequest_ForAnUnknownPanel(string? mode)
    {
        var (tickPage, _, _) = CreatePage(DraftProfile());
        tickPage.Input = new ProfileNoteInput { Mode = mode };
        var (untickPage, _, _) = CreatePage(DraftProfile());
        untickPage.Input = new ProfileNoteInput { Mode = mode };

        Assert.IsType<BadRequestResult>(await tickPage.OnPostTickAllAsync(CancellationToken.None));
        Assert.IsType<BadRequestResult>(await untickPage.OnPostUntickAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TickAll_ShowsNoPanel_WhenTheUserCannotEdit()
    {
        var (pageModel, _, _) = CreatePage(ReadOnlyProfile());
        pageModel.Input = new ProfileNoteInput { Mode = "add" };

        var result = await pageModel.OnPostTickAllAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
    }

    [Fact]
    public async Task TickAll_ShowsNoPanelAndReportsIt_WhenTheQuestionsCannotBeLoaded()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), failures: new Failures(Questions: new HttpRequestException("boom")));
        pageModel.Input = new ProfileNoteInput { Mode = "add" };

        var result = await pageModel.OnPostTickAllAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ProfileNotePanel.None, pageModel.Panel);
        Assert.Equal("We could not load the questions for this section. Try again later.", pageModel.ActionMessage);
    }

    [Fact]
    public async Task TickAll_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var (pageModel, _, _) = CreatePage(manageProfile: null);
        pageModel.Input = new ProfileNoteInput { Mode = "add" };

        Assert.IsType<NotFoundResult>(await pageModel.OnPostTickAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UrlBuilders_KeepTheProfileSectionPageSizeAndSort()
    {
        var notes = Enumerable.Range(1, 25).Select(index => Linked($"Note {index:00}", QuestionId)).ToList();
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);
        pageModel.PageNumber = 2;
        pageModel.SortColumn = "QuestionReference";
        pageModel.SortDirection = "Descending";
        await pageModel.OnGetAsync(CancellationToken.None);
        var noteId = Guid.NewGuid();
        var prefix = $"{FurtherInformationPage}?profileId={ProfileId}&section={SummarySectionId}";
        const string Paging = "pageNumber=2&pageSize=10&sortColumn=QuestionReference&sortDirection=Descending";

        Assert.Equal($"{prefix}&mode=add&noteId=&{Paging}", pageModel.BuildAddUrl());
        Assert.Equal($"{prefix}&mode=select&noteId=&{Paging}", pageModel.BuildSelectUrl());
        Assert.Equal($"{prefix}&mode=&noteId={noteId}&{Paging}", pageModel.BuildViewUrl(noteId));
        Assert.Equal($"{prefix}&mode=&noteId=&{Paging}", pageModel.BuildListUrl());
        Assert.Equal($"{prefix}&mode=&noteId=&pageNumber=3&pageSize=10&sortColumn=QuestionReference&sortDirection=Descending", pageModel.BuildListUrl(3));
    }

    [Theory]
    [InlineData("Reference", "Ascending", "Reference", "Descending")]
    [InlineData("Reference", "Descending", "Reference", "Ascending")]
    [InlineData("QuestionReference", "Descending", "Reference", "Ascending")]
    [InlineData("Reference", "Ascending", "QuestionReference", "Ascending")]
    public async Task BuildSortUrl_TogglesTheDirectionOfTheSortedColumn_AndReturnsToTheFirstPage(
        string currentColumn,
        string currentDirection,
        string clickedColumn,
        string expectedDirection)
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);
        pageModel.SortColumn = currentColumn;
        pageModel.SortDirection = currentDirection;
        await pageModel.OnGetAsync(CancellationToken.None);

        var url = pageModel.BuildSortUrl(clickedColumn);

        Assert.Equal(
            $"{FurtherInformationPage}?profileId={ProfileId}&section={SummarySectionId}&mode=&noteId=&pageNumber=1&pageSize=10&sortColumn={clickedColumn}&sortDirection={expectedDirection}",
            url);
    }

    [Theory]
    [InlineData("Reference", "Ascending", "Reference", "ascending")]
    [InlineData("Reference", "Descending", "Reference", "descending")]
    [InlineData("Reference", "Descending", "QuestionReference", "none")]
    [InlineData("QuestionReference", "Ascending", "questionreference", "ascending")]
    public async Task GetAriaSort_DescribesTheSortOfEachColumnHeader(string currentColumn, string currentDirection, string column, string expected)
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("Source", QuestionId)]);
        pageModel.SortColumn = currentColumn;
        pageModel.SortDirection = currentDirection;
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expected, pageModel.GetAriaSort(column));
    }
}
