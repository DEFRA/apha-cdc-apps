using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// The References tab holds two editors, one for each kind of reference. The editing rules themselves are tested through the Further
// information tab, which has one; these tests cover what is particular to a tab with two: the kinds are told apart, each keeps its own
// notes and its own paging and sort, and only the kind a request names is opened, changed or told of a result.
public class EditProfileReferencesModelTests
{
    private const string ReferencesPage = "/SurveillanceProfiles/EditProfileReferences";

    private static readonly Guid ScientificTypeId = Guid.NewGuid();
    private static readonly Guid LegislativeTypeId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly Guid ThirdQuestionId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly string RowVersionText = Convert.ToBase64String(RowVersion);

    // Which calls to CDC.Api the fake should fail, so the page's resilience can be tested.
    private sealed record Failures(Exception? NoteTypes = null, Exception? Questions = null, Exception? Change = null);

    // What each kind holds: the notes linked to the Summary section, and the notes of the whole version (its master list).
    private sealed record Notes(
        IReadOnlyList<ProfileNoteDto>? Scientific = null,
        IReadOnlyList<ProfileNoteDto>? ScientificMaster = null,
        IReadOnlyList<ProfileNoteDto>? Legislative = null,
        IReadOnlyList<ProfileNoteDto>? LegislativeMaster = null);

    private static ProfileNoteTypeDto ScientificType() =>
        new() { Id = ScientificTypeId, Name = "Scientific paper reference", PluralName = "Scientific paper references" };

    private static ProfileNoteTypeDto LegislativeType() =>
        new() { Id = LegislativeTypeId, Name = "Legislative reference", PluralName = "Legislative references" };

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

    private static (EditProfileReferencesModel PageModel, FakeProfileSectionsApiService Api, AlwaysEnabledLogger<EditProfileReferencesModel> Log) CreatePage(
        ManageProfileViewModel? manageProfile,
        Notes? notes = null,
        IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
        ProfileNoteChangeResult? changeResult = null,
        Failures? failures = null,
        Func<Guid, Task>? beforeSectionNotes = null,
        Func<Task>? beforeMasterList = null)
    {
        var log = new AlwaysEnabledLogger<EditProfileReferencesModel>();
        var failing = failures ?? new Failures();
        var held = notes ?? new Notes();
        var api = new FakeProfileSectionsApiService(
            metadata: Metadata(),
            noteTypes: noteTypes ?? [ScientificType(), LegislativeType()],
            notesByNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [ScientificTypeId] = held.Scientific ?? [],
                [LegislativeTypeId] = held.Legislative ?? []
            },
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
            {
                [ScientificTypeId] = held.ScientificMaster ?? held.Scientific ?? [],
                [LegislativeTypeId] = held.LegislativeMaster ?? held.Legislative ?? []
            },
            throwOnGetProfileNoteTypes: failing.NoteTypes,
            questionInfos: Questions(),
            throwOnGetProfileQuestionInfoList: failing.Questions,
            noteChangeResult: changeResult,
            throwOnUpdateProfileNotes: failing.Change,
            beforeGetProfileNotesBySection: beforeSectionNotes,
            beforeGetProfileNotesByVersion: beforeMasterList);

        var pageModel = Configure(new EditProfileReferencesModel(new FakeApiClient(manageProfile: manageProfile), api, log));

        return (pageModel, api, log);
    }

    private static ManageProfileViewModel ReadOnlyProfile() => DraftProfile() with { CanEditProfileSection = false };

    private static string UrlOf(string tail) => $"{ReferencesPage}?profileId={ProfileId}&section={SummarySectionId}{tail}";

    [Fact]
    public void ActiveTab_IsReferences() =>
        Assert.Equal(ProfileSectionTab.References, CreatePage(Profile()).PageModel.ActiveTab);

    [Fact]
    public void TheTab_HoldsAScientificPaperEditorAndALegislativeEditor()
    {
        var (pageModel, _, _) = CreatePage(Profile());

        Assert.Equal(["scientific", "legislative"], pageModel.Editors.Select(editor => editor.Key));
        Assert.Same(pageModel.Editors[0], pageModel.ScientificPaperReferences);
        Assert.Same(pageModel.Editors[1], pageModel.LegislativeReferences);
        Assert.All(pageModel.Editors, editor => Assert.True(editor.HasSiblings));
        Assert.Equal("profile-references-panel", pageModel.PanelId);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var (pageModel, _, _) = CreatePage(manageProfile: null);

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_ListsEachKindsNotesApart_MatchedByNoteTypeName_RegardlessOfCasingOrSpacing()
    {
        var noteTypes = new List<ProfileNoteTypeDto>
        {
            new() { Id = ScientificTypeId, Name = "scientific paper reference", PluralName = "Scientific paper references" },
            new() { Id = LegislativeTypeId, Name = "LegislativeReference", PluralName = "Legislative references" }
        };
        var notes = new Notes(
            Scientific: [Linked("Zebra et al. (2020) <a href='https://example.com'>https://example.com</a>", QuestionId), Linked("Aardvark et al. (2019)", QuestionId, SecondQuestionId)],
            Legislative: [Linked("Animal Health Act 1981", ThirdQuestionId)]);
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes, noteTypes);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("scientific paper reference", pageModel.ScientificPaperReferences.NoteTypeName);
        Assert.Equal(["Aardvark et al. (2019)", "Zebra et al. (2020) https://example.com"], pageModel.ScientificPaperReferences.Rows.Select(row => row.PlainText));
        Assert.Equal(["1.1, 1.2", "1.1"], pageModel.ScientificPaperReferences.Rows.Select(row => row.QuestionReferenceDisplay));
        Assert.Contains("<a href=", pageModel.ScientificPaperReferences.Rows[1].TextHtml);
        Assert.Equal("LegislativeReference", pageModel.LegislativeReferences.NoteTypeName);
        Assert.Equal(["Animal Health Act 1981"], pageModel.LegislativeReferences.Rows.Select(row => row.PlainText));
        Assert.Equal(["1.3"], pageModel.LegislativeReferences.Rows.Select(row => row.QuestionReferenceDisplay));
    }

    [Fact]
    public async Task OnGetAsync_LabelsEachKindInTheLegacyWords()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("Add a new scientific paper reference:", pageModel.ScientificPaperReferences.AddLabel);
        Assert.Equal("Select a scientific paper reference from the master list:", pageModel.ScientificPaperReferences.SelectLabel);
        Assert.Equal("There are no scientific paper references to display.", pageModel.ScientificPaperReferences.NoNotesMessage);
        Assert.Equal("Are you sure you want to delete the association to this scientific paper reference?", pageModel.ScientificPaperReferences.DeleteConfirmation);
        Assert.Equal("Add a new legislative reference:", pageModel.LegislativeReferences.AddLabel);
        Assert.Equal("Select a legislative reference from the master list:", pageModel.LegislativeReferences.SelectLabel);
        Assert.Equal("There are no legislative references to display.", pageModel.LegislativeReferences.NoNotesMessage);
        Assert.Equal("Are you sure you want to delete the association to this legislative reference?", pageModel.LegislativeReferences.DeleteConfirmation);
    }

    [Fact]
    public async Task OnGetAsync_ReadsBothKindsNotesAtOnce()
    {
        var legislativeAsked = new TaskCompletionSource();
        var notes = new Notes(Scientific: [Linked("Aardvark et al. (2019)", QuestionId)], Legislative: [Linked("Animal Health Act 1981", QuestionId)]);

        // The scientific paper references cannot finish until the legislative ones have been asked for, so reading them one after the other would time out.
        Task BeforeNotesAreRead(Guid noteTypeId)
        {
            if (noteTypeId == LegislativeTypeId)
            {
                legislativeAsked.TrySetResult();

                return Task.CompletedTask;
            }

            return legislativeAsked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var (pageModel, _, _) = CreatePage(DraftProfile(), notes, beforeSectionNotes: BeforeNotesAreRead);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Single(pageModel.ScientificPaperReferences.Rows);
        Assert.Single(pageModel.LegislativeReferences.Rows);
    }

    [Fact]
    public async Task OnGetAsync_KeepsTheScientificPaperReferences_WhenTheLegislativeReferencesCannotBeRead()
    {
        var notes = new Notes(Scientific: [Linked("Aardvark et al. (2019)", QuestionId)], Legislative: [Linked("Animal Health Act 1981", QuestionId)]);

        Task BeforeNotesAreRead(Guid noteTypeId) =>
            noteTypeId == LegislativeTypeId ? Task.FromException(new HttpRequestException("connection refused")) : Task.CompletedTask;

        var (pageModel, _, log) = CreatePage(DraftProfile(), notes, beforeSectionNotes: BeforeNotesAreRead);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Single(pageModel.ScientificPaperReferences.Rows);
        Assert.True(pageModel.ScientificPaperReferences.CanEdit);
        Assert.False(pageModel.ScientificPaperReferences.HasFormErrors);
        Assert.Empty(pageModel.LegislativeReferences.Rows);
        Assert.False(pageModel.LegislativeReferences.CanEdit);
        Assert.Equal("We could not load the legislative references. Try again later.", pageModel.LegislativeReferences.ActionMessage);
        Assert.True(pageModel.HasFormErrors);
        Assert.Contains(log.Messages, message => message.StartsWith("Failed to load profile", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_ReportsBothKindsAsNotLoaded_WhenTheNoteTypesCannotBeRead()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), failures: new Failures(NoteTypes: new HttpRequestException("connection refused")));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.HasError);
        Assert.Equal("We could not load the scientific paper references. Try again later.", pageModel.ScientificPaperReferences.ActionMessage);
        Assert.Equal("We could not load the legislative references. Try again later.", pageModel.LegislativeReferences.ActionMessage);
        Assert.All(pageModel.Editors, editor => Assert.False(editor.CanEdit));
        Assert.All(pageModel.Editors, editor => Assert.Empty(editor.Rows));
    }

    [Fact]
    public async Task OnGetAsync_ShowsEmptyTablesWithNoEditing_WhenTheNoteTypesAreNotFound()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), noteTypes: []);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.All(pageModel.Editors, editor => Assert.Empty(editor.Rows));
        Assert.All(pageModel.Editors, editor => Assert.False(editor.CanEdit));
        Assert.All(pageModel.Editors, editor => Assert.Null(editor.ActionMessage));
        Assert.Equal("Scientific paper reference", pageModel.ScientificPaperReferences.NoteTypeName);
        Assert.Equal("There are no scientific paper references to display.", pageModel.ScientificPaperReferences.NoNotesMessage);
    }

    [Fact]
    public async Task OnGetAsync_LoadsNothing_WhenTheProfileHasNoVersion()
    {
        var (pageModel, api, _) = CreatePage(Profile(currentProfileVersionId: Guid.Empty), new Notes(Scientific: [Linked("Source", QuestionId)]));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasNoVersion);
        Assert.All(pageModel.Editors, editor => Assert.Empty(editor.Rows));
        Assert.Null(api.RequestedQuestionInfoSectionId);
    }

    [Fact]
    public async Task OnGetAsync_ListsForSelecting_OnlyTheMasterListNotesNotYetLinkedToTheSection_OfEachKind()
    {
        var inSection = Linked("In the section", QuestionId);
        var notes = new Notes(
            Scientific: [inSection],
            ScientificMaster: [inSection, Unlinked("Linked elsewhere")],
            Legislative: [Linked("Act", QuestionId)]);
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.ScientificPaperReferences.CanEdit);
        Assert.Equal(["Linked elsewhere"], pageModel.ScientificPaperReferences.AvailableNoteChoices.Select(choice => choice.Text));
        Assert.True(pageModel.LegislativeReferences.CanEdit);
        Assert.Empty(pageModel.LegislativeReferences.AvailableNoteChoices);
    }

    // A note deleted from a section is only unlinked from it, so it is still in the master list for the section to select again.
    [Fact]
    public async Task OnGetAsync_OffersANoteWithNoLinksInTheSectionForSelecting_EvenThoughItsKindHasOthersLinked()
    {
        var unlinkedNow = Note("Deleted from the section earlier");
        var stillLinked = Linked("Still linked", QuestionId);
        var notes = new Notes(Legislative: [stillLinked], LegislativeMaster: [stillLinked, unlinkedNow]);
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.LegislativeReferences.CanEdit);
        Assert.Equal(unlinkedNow.Id, Assert.Single(pageModel.LegislativeReferences.AvailableNoteChoices).Id);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheTablesButNoEditing_WhenTheUserCannotEdit()
    {
        var masterListRead = false;
        var notes = new Notes(Scientific: [Linked("Source", QuestionId)], ScientificMaster: [Linked("Source", QuestionId), Unlinked("Other")]);
        var (pageModel, api, _) = CreatePage(
            ReadOnlyProfile(),
            notes,
            beforeMasterList: () =>
            {
                masterListRead = true;

                return Task.CompletedTask;
            });
        pageModel.Mode = "add";
        pageModel.NoteTypeKey = "scientific";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Single(pageModel.ScientificPaperReferences.Rows);
        Assert.All(pageModel.Editors, editor => Assert.False(editor.CanEdit));
        Assert.All(pageModel.Editors, editor => Assert.Empty(editor.AvailableNoteChoices));
        Assert.All(pageModel.Editors, editor => Assert.Equal(ProfileNotePanel.None, editor.Panel));
        Assert.False(masterListRead);
        Assert.Null(api.RequestedQuestionInfoSectionId);
    }

    [Theory]
    [InlineData("scientific", "Add a scientific paper reference")]
    [InlineData("SCIENTIFIC", "Add a scientific paper reference")]
    [InlineData("legislative", "Add a legislative reference")]
    [InlineData("Legislative", "Add a legislative reference")]
    public async Task OnGetAsync_OpensTheAddPanelOfTheKindNamedByType_AndNoOther(string type, string heading)
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Mode = "add";
        pageModel.NoteTypeKey = type;

        await pageModel.OnGetAsync(CancellationToken.None);

        var opened = Assert.Single(pageModel.Editors, editor => editor.Panel != ProfileNotePanel.None);
        Assert.Equal(heading, opened.PanelHeading);
        Assert.Equal(ProfileNotePanel.Add, opened.Panel);
        Assert.Equal(["1.1", "1.2", "1.3"], opened.QuestionChoices.Select(choice => choice.Number));
        Assert.Equal(SummarySectionId, api.RequestedQuestionInfoSectionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    public async Task OnGetAsync_OpensNoPanel_WhenNoKindOrAnUnknownKindIsNamed(string? type)
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Mode = "add";
        pageModel.NoteTypeKey = type;
        pageModel.Notice = "saved";

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.All(pageModel.Editors, editor => Assert.Equal(ProfileNotePanel.None, editor.Panel));
        Assert.All(pageModel.Editors, editor => Assert.Null(editor.ActionMessage));
        Assert.Null(api.RequestedQuestionInfoSectionId);
    }

    [Fact]
    public async Task OnGetAsync_OpensTheSelectPanelOfTheNamedKind_OfferingThatKindsMasterListOnly()
    {
        var scientific = Unlinked("Scientific only");
        var legislative = Unlinked("Legislative only");
        var (pageModel, _, _) = CreatePage(DraftProfile(), new Notes(ScientificMaster: [scientific], LegislativeMaster: [legislative]));
        pageModel.Mode = "select";
        pageModel.NoteTypeKey = "legislative";
        pageModel.NoteId = legislative.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.None, pageModel.ScientificPaperReferences.Panel);
        Assert.Equal(ProfileNotePanel.Select, pageModel.LegislativeReferences.Panel);
        Assert.Equal("Select a legislative reference", pageModel.LegislativeReferences.PanelHeading);
        Assert.Equal("Legislative only", pageModel.LegislativeReferences.PanelNoteHtml);
        Assert.Equal(legislative.Id, Assert.Single(pageModel.LegislativeReferences.AvailableNoteChoices, choice => choice.IsSelected).Id);
        Assert.Equal(["Scientific only"], pageModel.ScientificPaperReferences.AvailableNoteChoices.Select(choice => choice.Text));
    }

    // Select is offered whatever the master list holds, as the legacy page did once Add was cancelled, so its panel opens with an empty drop-down.
    [Fact]
    public async Task OnGetAsync_OpensTheSelectPanelOfTheNamedKind_WithAnEmptyDropDown_WhenItsMasterListHoldsNothingToSelect()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), new Notes(Legislative: [Linked("Act", QuestionId)]));
        pageModel.Mode = "select";
        pageModel.NoteTypeKey = "legislative";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.Select, pageModel.LegislativeReferences.Panel);
        Assert.Empty(pageModel.LegislativeReferences.AvailableNoteChoices);
        Assert.Null(pageModel.LegislativeReferences.PanelNoteHtml);
        Assert.Equal(ProfileNotePanel.None, pageModel.ScientificPaperReferences.Panel);
    }

    [Fact]
    public async Task OnGetAsync_OpensTheViewPanelOfTheNamedKind_WithTheQuestionsTheNoteIsLinkedToTicked()
    {
        var reference = Linked("<p>A paper</p>", QuestionId, SecondQuestionId);
        var (pageModel, _, _) = CreatePage(DraftProfile(), new Notes(Scientific: [reference], Legislative: [Linked("An act", QuestionId)]));
        pageModel.NoteTypeKey = "scientific";
        pageModel.NoteId = reference.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfileNotePanel.View, pageModel.ScientificPaperReferences.Panel);
        Assert.Equal("View a scientific paper reference", pageModel.ScientificPaperReferences.PanelHeading);
        Assert.Equal([true, true, false], pageModel.ScientificPaperReferences.QuestionChoices.Select(choice => choice.IsTicked));
        Assert.Equal(ProfileNotePanel.None, pageModel.LegislativeReferences.Panel);
    }

    [Fact]
    public async Task OnGetAsync_OpensNoPanel_WhenTheViewedNoteIsOfTheOtherKind()
    {
        var act = Linked("An act", QuestionId);
        var (pageModel, _, _) = CreatePage(DraftProfile(), new Notes(Scientific: [Linked("A paper", QuestionId)], Legislative: [act]));
        pageModel.NoteTypeKey = "scientific";
        pageModel.NoteId = act.Id;

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.All(pageModel.Editors, editor => Assert.Equal(ProfileNotePanel.None, editor.Panel));
    }

    [Theory]
    [InlineData("scientific", "Scientific paper reference save succeeded", null)]
    [InlineData("legislative", null, "Legislative reference save succeeded")]
    public async Task OnGetAsync_ShowsTheResultOfThePreviousChange_ForTheKindThatWasChanged(string type, string? scientificMessage, string? legislativeMessage)
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.NoteTypeKey = type;
        pageModel.Notice = "saved";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(scientificMessage, pageModel.ScientificPaperReferences.ActionMessage);
        Assert.Equal(legislativeMessage, pageModel.LegislativeReferences.ActionMessage);
        Assert.False(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task HasFormErrors_IsTrueWhenEitherKindReportsAProblem()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.NoteTypeKey = "legislative";
        pageModel.Input = new ProfileNoteInput { Mode = "add" };

        Assert.False(pageModel.HasFormErrors);

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.True(pageModel.HasFormErrors);
        Assert.False(pageModel.ScientificPaperReferences.HasFormErrors);
        Assert.True(pageModel.LegislativeReferences.HasFormErrors);
    }

    [Theory]
    [InlineData("scientific", "Scientific paper reference")]
    [InlineData("legislative", "Legislative reference")]
    public async Task OnPostSaveAsync_Add_InsertsTheNoteAsTheNamedKind_AndRedirectsKeepingEveryTablesPagingAndSort(string type, string expectedNoteTypeName)
    {
        var expectedTypeId = type == "scientific" ? ScientificTypeId : LegislativeTypeId;
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.NoteTypeKey = type;
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "<p>A brand new one</p>", QuestionIds = [QuestionId, SecondQuestionId] };
        pageModel.ScientificPaperList = new ProfileNoteListState { PageSize = "25", SortColumn = "QuestionReference", SortDirection = "Descending" };
        pageModel.LegislativeList = new ProfileNoteListState { PageSize = "50" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(ReferencesPage, redirect.PageName);
        Assert.Equal(ProfileId, redirect.RouteValues!["profileId"]);
        Assert.Equal(SummarySectionId, redirect.RouteValues["section"]);
        Assert.Equal(type, redirect.RouteValues["type"]);
        Assert.Equal("saved", redirect.RouteValues["result"]);
        Assert.Equal(1, redirect.RouteValues["scientific.pageNumber"]);
        Assert.Equal("25", redirect.RouteValues["scientific.pageSize"]);
        Assert.Equal("QuestionReference", redirect.RouteValues["scientific.sortColumn"]);
        Assert.Equal("Descending", redirect.RouteValues["scientific.sortDirection"]);
        Assert.Equal("50", redirect.RouteValues["legislative.pageSize"]);
        Assert.Equal("Reference", redirect.RouteValues["legislative.sortColumn"]);
        Assert.False(redirect.RouteValues.ContainsKey("mode"));
        Assert.False(redirect.RouteValues.ContainsKey("noteId"));
        Assert.Equal(ProfileVersionId, api.ChangedNotesProfileVersionId);
        Assert.Equal(expectedTypeId, api.ChangedNotesNoteTypeId);
        Assert.Equal([expectedTypeId], api.NoteTypesChanged);
        var insert = Assert.Single(api.SavedNoteInserts!);
        Assert.Equal("<p>A brand new one</p>", insert.NoteText);
        Assert.Equal([QuestionId, SecondQuestionId], insert.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
        Assert.Equal(expectedNoteTypeName, pageModel.Editors.Single(editor => editor.Key == type).NoteTypeName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bogus")]
    public async Task OnPostSaveAsync_ReturnsBadRequest_WhenNoKindOrAnUnknownKindIsNamed(string? type)
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.NoteTypeKey = type;
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "Text", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsBadRequest_ForAnUnknownPanelOfTheNamedKind()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.NoteTypeKey = "scientific";
        pageModel.Input = new ProfileNoteInput { Mode = "bogus" };

        Assert.IsType<BadRequestResult>(await pageModel.OnPostSaveAsync(CancellationToken.None));
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReportsTheProblemOnTheNamedKindOnly_AndStillShowsBothTables()
    {
        var notes = new Notes(Scientific: [Linked("A paper", QuestionId)], Legislative: [Linked("An act", QuestionId)]);
        var (pageModel, api, _) = CreatePage(DraftProfile(), notes);
        pageModel.NoteTypeKey = "scientific";
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "<p>&nbsp;</p>" };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(
            [
                new ProfileNoteFormError("Input_NoteText", "Please enter a reference"),
                new ProfileNoteFormError("Input_QuestionIds_0", "At least one question must be selected")
            ],
            pageModel.ScientificPaperReferences.FormErrors);
        Assert.Equal(ProfileNotePanel.Add, pageModel.ScientificPaperReferences.Panel);
        Assert.Empty(pageModel.LegislativeReferences.FormErrors);
        Assert.Equal(ProfileNotePanel.None, pageModel.LegislativeReferences.Panel);
        Assert.Single(pageModel.ScientificPaperReferences.Rows);
        Assert.Single(pageModel.LegislativeReferences.Rows);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_Select_LinksTheChosenNoteOfTheNamedKind()
    {
        var available = Unlinked("<p>Linked elsewhere</p>");
        var (pageModel, api, _) = CreatePage(DraftProfile(), new Notes(LegislativeMaster: [available]));
        pageModel.NoteTypeKey = "legislative";
        pageModel.Input = new ProfileNoteInput { Mode = "select", NoteId = available.Id, LastUpdated = RowVersionText, QuestionIds = [SecondQuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("legislative", redirect.RouteValues!["type"]);
        Assert.Equal(LegislativeTypeId, api.ChangedNotesNoteTypeId);
        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.Equal(available.Id, update.Id);
        Assert.Equal([SecondQuestionId], update.QuestionReferenceAdds.Select(reference => reference.ProfileQuestionId));
    }

    [Fact]
    public async Task OnPostSaveAsync_Select_RejectsANoteThatBelongsToTheOtherKind()
    {
        var scientific = Unlinked("A paper linked elsewhere");
        var (pageModel, api, _) = CreatePage(DraftProfile(), new Notes(ScientificMaster: [scientific], LegislativeMaster: [Unlinked("An act")]));
        pageModel.NoteTypeKey = "legislative";
        pageModel.Input = new ProfileNoteInput { Mode = "select", NoteId = scientific.Id, LastUpdated = RowVersionText, QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal([new ProfileNoteFormError("NoteId", "Please select a reference")], pageModel.LegislativeReferences.FormErrors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReportsAFailureOfTheChangeOnTheNamedKind_UsingItsName()
    {
        var (pageModel, _, _) = CreatePage(
            DraftProfile(),
            changeResult: new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Conflict, ErrorMessage = "Another user changed it." });
        pageModel.NoteTypeKey = "legislative";
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "<p>Typed text</p>", QuestionIds = [QuestionId] };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Legislative reference save failed: Another user changed it.", pageModel.LegislativeReferences.ActionMessage);
        Assert.Null(pageModel.ScientificPaperReferences.ActionMessage);
        Assert.Equal(ProfileNotePanel.Add, pageModel.LegislativeReferences.Panel);
        Assert.True(pageModel.HasFormErrors);
    }

    [Fact]
    public async Task OnPostSaveAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var (pageModel, _, _) = CreatePage(manageProfile: null);
        pageModel.NoteTypeKey = "scientific";

        Assert.IsType<NotFoundResult>(await pageModel.OnPostSaveAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnPostDeleteAsync_UnlinksTheNoteFromTheNamedKindOnly_AndRedirects()
    {
        var act = Linked("An act", QuestionId, SecondQuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), new Notes(Scientific: [Linked("A paper", QuestionId)], Legislative: [act]));
        pageModel.NoteTypeKey = "legislative";

        var result = await pageModel.OnPostDeleteAsync(act.Id, RowVersionText, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("legislative", redirect.RouteValues!["type"]);
        Assert.Equal("deleted", redirect.RouteValues["result"]);
        Assert.Equal(LegislativeTypeId, api.ChangedNotesNoteTypeId);
        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.Equal(act.Id, update.Id);
        Assert.Empty(update.QuestionReferenceAdds);
        Assert.Equal(
            new[] { QuestionId, SecondQuestionId }.Order(),
            update.QuestionReferenceRemoves.Select(reference => reference.ProfileQuestionId).Order());
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReportsTheNoteNoLongerExisting_WhenItBelongsToTheOtherKind()
    {
        var paper = Linked("A paper", QuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), new Notes(Scientific: [paper], Legislative: [Linked("An act", QuestionId)]));
        pageModel.NoteTypeKey = "legislative";

        var result = await pageModel.OnPostDeleteAsync(paper.Id, RowVersionText, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Legislative reference deletion failed: The legislative reference no longer exists.", pageModel.LegislativeReferences.ActionMessage);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ReturnsBadRequest_WhenNoKindIsNamed()
    {
        var note = Linked("A paper", QuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), new Notes(Scientific: [note]));

        var result = await pageModel.OnPostDeleteAsync(note.Id, RowVersionText, CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData("scientific", true)]
    [InlineData("legislative", false)]
    public async Task TickAllAndUntickAll_ChangeTheTicksOfTheNamedKindsPanel(string type, bool tickAll)
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.NoteTypeKey = type;
        pageModel.Input = new ProfileNoteInput { Mode = "add", NoteText = "<p>keep me</p>", QuestionIds = [QuestionId] };

        var result = tickAll
            ? await pageModel.OnPostTickAllAsync(CancellationToken.None)
            : await pageModel.OnPostUntickAllAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        var shown = Assert.Single(pageModel.Editors, editor => editor.Panel != ProfileNotePanel.None);
        Assert.Equal(type, shown.Key);
        Assert.Equal(tickAll ? 3 : 0, shown.QuestionChoices.Count(choice => choice.IsTicked));
        Assert.Equal("<p>keep me</p>", shown.Input.NoteText);
    }

    [Fact]
    public async Task TickAll_ReturnsBadRequest_WhenNoKindIsNamed()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.Input = new ProfileNoteInput { Mode = "add" };

        Assert.IsType<BadRequestResult>(await pageModel.OnPostTickAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_KeepsEachTablesPageAndSortApart()
    {
        var notes = new Notes(
            Scientific: [.. Enumerable.Range(1, 12).Select(index => Linked($"Paper {index:00}", QuestionId))],
            Legislative: [Linked("Zebra Act", ThirdQuestionId), Linked("Aardvark Act", QuestionId)]);
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);
        pageModel.ScientificPaperList.PageNumber = 2;
        pageModel.LegislativeList.SortColumn = "QuestionReference";
        pageModel.LegislativeList.SortDirection = "Descending";

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, pageModel.ScientificPaperReferences.PageNumber);
        Assert.Equal(2, pageModel.ScientificPaperReferences.TotalPages);
        Assert.Equal(["Paper 11", "Paper 12"], pageModel.ScientificPaperReferences.Rows.Select(row => row.PlainText));
        Assert.Equal("Reference", pageModel.ScientificPaperReferences.SortColumn);
        Assert.Equal(1, pageModel.LegislativeReferences.PageNumber);
        Assert.Equal(1, pageModel.LegislativeReferences.TotalPages);
        Assert.Equal(["Zebra Act", "Aardvark Act"], pageModel.LegislativeReferences.Rows.Select(row => row.PlainText));
        Assert.Equal("QuestionReference", pageModel.LegislativeReferences.SortColumn);
        Assert.Equal("Descending", pageModel.LegislativeReferences.SortDirection);
    }

    [Fact]
    public async Task OnGetAsync_NormalisesEachTablesPagingAndSortSeparately()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), new Notes(Scientific: [Linked("Paper", QuestionId)], Legislative: [Linked("Act", QuestionId)]));
        pageModel.ScientificPaperList = new ProfileNoteListState { PageNumber = 99, PageSize = "7", SortColumn = "Bogus", SortDirection = "Sideways" };
        pageModel.LegislativeList = new ProfileNoteListState { PageNumber = -3, PageSize = "25", SortColumn = "questionreference", SortDirection = "descending" };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal((1, "10", "Reference", "Ascending"), Normalised(pageModel.ScientificPaperReferences));
        Assert.Equal((1, "25", "QuestionReference", "Descending"), Normalised(pageModel.LegislativeReferences));
    }

    private static (int Page, string Size, string Column, string Direction) Normalised(ProfileNoteEditor editor) =>
        (editor.PageNumber, editor.PageSize, editor.SortColumn, editor.SortDirection);

    [Fact]
    public async Task UrlBuilders_NameTheKindOfAPanelLink_AndKeepEveryTablesPagingAndSort()
    {
        var notes = new Notes(
            Scientific: [.. Enumerable.Range(1, 25).Select(index => Linked($"Paper {index:00}", QuestionId))],
            Legislative: [Linked("Act", QuestionId)]);
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);
        pageModel.ScientificPaperList.PageNumber = 2;
        pageModel.ScientificPaperList.SortColumn = "QuestionReference";
        pageModel.ScientificPaperList.SortDirection = "Descending";
        pageModel.LegislativeList.PageSize = "25";
        await pageModel.OnGetAsync(CancellationToken.None);
        var noteId = Guid.NewGuid();
        const string Scientific = "scientific.pageNumber=2&scientific.pageSize=10&scientific.sortColumn=QuestionReference&scientific.sortDirection=Descending";
        const string Legislative = "legislative.pageNumber=1&legislative.pageSize=25&legislative.sortColumn=Reference&legislative.sortDirection=Ascending";

        Assert.Equal(UrlOf($"&mode=add&noteId=&type=scientific&{Scientific}&{Legislative}"), pageModel.ScientificPaperReferences.BuildAddUrl());
        Assert.Equal(UrlOf($"&mode=add&noteId=&type=legislative&{Scientific}&{Legislative}"), pageModel.LegislativeReferences.BuildAddUrl());
        Assert.Equal(UrlOf($"&mode=select&noteId=&type=legislative&{Scientific}&{Legislative}"), pageModel.LegislativeReferences.BuildSelectUrl());
        Assert.Equal(UrlOf($"&mode=&noteId={noteId}&type=scientific&{Scientific}&{Legislative}"), pageModel.ScientificPaperReferences.BuildViewUrl(noteId));
    }

    [Fact]
    public async Task UrlBuilders_NameNoKindOfALinkThatOpensNoPanel_AndChangeOnlyTheTableTheyBelongTo()
    {
        var notes = new Notes(
            Scientific: [.. Enumerable.Range(1, 25).Select(index => Linked($"Paper {index:00}", QuestionId))],
            Legislative: [Linked("Act", QuestionId)]);
        var (pageModel, _, _) = CreatePage(DraftProfile(), notes);
        pageModel.ScientificPaperList.PageNumber = 2;
        await pageModel.OnGetAsync(CancellationToken.None);
        const string Legislative = "legislative.pageNumber=1&legislative.pageSize=10&legislative.sortColumn=Reference&legislative.sortDirection=Ascending";

        Assert.Equal(
            UrlOf($"&mode=&noteId=&scientific.pageNumber=3&scientific.pageSize=10&scientific.sortColumn=Reference&scientific.sortDirection=Ascending&{Legislative}"),
            pageModel.ScientificPaperReferences.BuildListUrl(3));
        Assert.Equal(
            UrlOf($"&mode=&noteId=&scientific.pageNumber=1&scientific.pageSize=10&scientific.sortColumn=QuestionReference&scientific.sortDirection=Ascending&{Legislative}"),
            pageModel.ScientificPaperReferences.BuildSortUrl("QuestionReference"));
        Assert.Equal(
            UrlOf("&mode=&noteId=&scientific.pageNumber=2&scientific.pageSize=10&scientific.sortColumn=Reference&scientific.sortDirection=Ascending&legislative.pageNumber=1&legislative.pageSize=10&legislative.sortColumn=Reference&legislative.sortDirection=Descending"),
            pageModel.LegislativeReferences.BuildSortUrl("Reference"));
    }

    [Fact]
    public async Task GetAriaSort_DescribesTheSortOfEachTablesColumnHeaders()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), new Notes(Scientific: [Linked("Paper", QuestionId)], Legislative: [Linked("Act", QuestionId)]));
        pageModel.LegislativeList.SortColumn = "QuestionReference";
        pageModel.LegislativeList.SortDirection = "Descending";
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("ascending", pageModel.ScientificPaperReferences.GetAriaSort("Reference"));
        Assert.Equal("none", pageModel.ScientificPaperReferences.GetAriaSort("QuestionReference"));
        Assert.Equal("none", pageModel.LegislativeReferences.GetAriaSort("Reference"));
        Assert.Equal("descending", pageModel.LegislativeReferences.GetAriaSort("QuestionReference"));
    }

    [Fact]
    public async Task StateFields_CarryTheSectionAndEveryTablesPagingAndSort_AndNameTheKindOnlyWhenAsked()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), new Notes(Scientific: [Linked("Paper", QuestionId)]));
        pageModel.LegislativeList.SortDirection = "Descending";
        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(
            [
                new("section", SummarySectionId.ToString()),
                new("scientific.pageNumber", "1"),
                new("scientific.pageSize", "10"),
                new("scientific.sortColumn", "Reference"),
                new("scientific.sortDirection", "Ascending"),
                new("legislative.pageNumber", "1"),
                new("legislative.pageSize", "10"),
                new("legislative.sortColumn", "Reference"),
                new("legislative.sortDirection", "Descending")
            ],
            pageModel.ScientificPaperReferences.StateFields());
        Assert.Equal(
            [
                new KeyValuePair<string, string>("section", SummarySectionId.ToString()),
                new("scientific.sortColumn", "Reference"),
                new("scientific.sortDirection", "Ascending"),
                new("legislative.pageNumber", "1"),
                new("legislative.pageSize", "10"),
                new("legislative.sortColumn", "Reference"),
                new("legislative.sortDirection", "Descending")
            ],
            pageModel.ScientificPaperReferences.StateFields(exceptOwnPaging: true));
        Assert.Equal("legislative.pageSize", pageModel.LegislativeReferences.FieldName("pageSize"));
    }
}
