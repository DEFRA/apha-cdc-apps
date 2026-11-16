using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// The "Add/Select a reference to question" dialog of the Questions tab, as the legacy EditProfileQuestions.aspx pop-up behaved: it offers
// the version's references of a type, and saves a new or existing reference against the question and the others the user picked.
public class QuestionReferencesModelTests
{
    private static readonly Guid NoteTypeId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly Guid ThirdQuestionId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly string RowVersionText = Convert.ToBase64String(RowVersion);

    private static ProfileQuestionMetadataDto QuestionMetadata(Guid id, int number) =>
        new() { Id = id, SectionId = SummarySectionId, ShortName = $"Question {number}", QuestionNumber = number, Fields = [] };

    // The Summary section holds three questions (1.1 to 1.3) so that a reference can be applied to several of them.
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

    private static ProfileNoteDto Note(string text, params QuestionReferenceDto[] references) =>
        new() { Id = Guid.NewGuid(), NoteText = text, LastUpdated = RowVersion, QuestionReferences = references };

    private static QuestionReferenceDto Reference(Guid questionId, Guid? sectionId = null) =>
        new() { ProfileSectionId = sectionId ?? SummarySectionId, ProfileQuestionId = questionId };

    private static ProfileNoteDto Linked(string text, params Guid[] questionIds) =>
        Note(text, [.. questionIds.Select(questionId => Reference(questionId))]);

    private static (QuestionReferencesModel PageModel, FakeProfileSectionsApiService Api, AlwaysEnabledLogger<QuestionReferencesModel> Log) CreatePage(
        ManageProfileViewModel? manageProfile,
        IReadOnlyList<ProfileNoteDto>? notes = null,
        ProfileNoteChangeResult? changeResult = null,
        Exception? throwOnGetNotes = null,
        Exception? throwOnChange = null,
        Exception? throwOnGetMetadata = null,
        Exception? throwOnGetManageProfile = null)
    {
        var log = new AlwaysEnabledLogger<QuestionReferencesModel>();
        var api = new FakeProfileSectionsApiService(
            metadata: Metadata(),
            notesByVersionAndNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [NoteTypeId] = notes ?? [] },
            throwOnGetProfileNotesByVersion: throwOnGetNotes,
            noteChangeResult: changeResult,
            throwOnUpdateProfileNotes: throwOnChange,
            throwOnGetProfileQuestionnaireMetadata: throwOnGetMetadata);

        var pageModel = Configure(
            new QuestionReferencesModel(new FakeApiClient(manageProfile: manageProfile, throwOnGetManageProfile: throwOnGetManageProfile), api, log),
            section: SummarySectionId);

        return (pageModel, api, log);
    }

    private static QuestionReferenceInput AddInput(string noteText = "<p>New reference</p>", params Guid[] alsoQuestionIds) => new()
    {
        QuestionId = QuestionId,
        Mode = QuestionReferencesModel.AddMode,
        NoteTypeId = NoteTypeId,
        NoteText = noteText,
        QuestionIds = [.. alsoQuestionIds]
    };

    private static QuestionReferenceInput SelectInput(Guid noteId, params Guid[] alsoQuestionIds) => new()
    {
        QuestionId = QuestionId,
        Mode = QuestionReferencesModel.SelectMode,
        NoteTypeId = NoteTypeId,
        NoteId = noteId,
        LastUpdated = RowVersionText,
        QuestionIds = [.. alsoQuestionIds]
    };

    private static QuestionReferenceSaveResponse SaveResponse(IActionResult result) =>
        Assert.IsType<QuestionReferenceSaveResponse>(Assert.IsType<JsonResult>(result).Value);

    private static void AssertSection(IEnumerable<QuestionReferenceDto> references) =>
        Assert.All(references, reference => Assert.Equal(SummarySectionId, reference.ProfileSectionId));

    // The order the questions are linked in does not matter, and GUIDs have no order of their own, so they are compared sorted.
    private static void AssertQuestions(IEnumerable<Guid> expected, IEnumerable<QuestionReferenceDto> actual) =>
        Assert.Equal(expected.Order(), actual.Select(reference => reference.ProfileQuestionId).Order());

    private static void AssertStatus(int expected, IActionResult result) =>
        Assert.Equal(expected, Assert.IsType<StatusCodeResult>(result).StatusCode);

    [Fact]
    public async Task OnGetNotesAsync_ListsTheVersionsReferencesOfTheType_AlphabeticallyWithTheirLinksInThisSection()
    {
        var zebra = Linked("<p>Zebra paper</p>", QuestionId);
        var apple = Note("Apple paper", Reference(SecondQuestionId), Reference(Guid.NewGuid(), EpidemiologySectionId));
        var (pageModel, _, _) = CreatePage(DraftProfile(), [zebra, apple]);

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        var options = Assert.IsAssignableFrom<IReadOnlyList<QuestionReferenceNoteOption>>(Assert.IsType<JsonResult>(result).Value);
        Assert.Equal(["Apple paper", "Zebra paper"], options.Select(option => option.Text));
        Assert.Equal([apple.Id, zebra.Id], options.Select(option => option.Id));
        Assert.Equal([SecondQuestionId], options[0].QuestionIds);
        Assert.Equal([QuestionId], options[1].QuestionIds);
        Assert.All(options, option => Assert.Equal(RowVersionText, option.LastUpdated));
    }

    [Fact]
    public async Task OnGetNotesAsync_ShortensTheTextOfALongReference_ButKeepsItsFullHtml()
    {
        var text = new string('a', 80);
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked($"<p>{text}</p>")]);

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        var option = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<QuestionReferenceNoteOption>>(Assert.IsType<JsonResult>(result).Value));
        Assert.Equal(new string('a', 47) + "...", option.Text);
        Assert.Equal($"<p>{text}</p>", option.Html);
    }

    [Fact]
    public async Task OnGetNotesAsync_DropsScriptsAndUnsafeMarkupFromTheHtmlOfAReference()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), [Linked("<p onclick=\"x()\">Paper</p><script>alert(1)</script>")]);

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        var option = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<QuestionReferenceNoteOption>>(Assert.IsType<JsonResult>(result).Value));
        Assert.Equal("<p>Paper</p>", option.Html);
    }

    [Fact]
    public async Task OnGetNotesAsync_ListsNothing_WhenTheTypeHasNoReferences()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<QuestionReferenceNoteOption>>(Assert.IsType<JsonResult>(result).Value));
    }

    [Fact]
    public async Task OnGetNotesAsync_IsForbidden_ToAUserWhoCannotEditTheSection()
    {
        var (pageModel, _, _) = CreatePage(ReadOnlyDraftProfile(), [Linked("Paper")]);

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        AssertStatus(StatusCodes.Status403Forbidden, result);
    }

    [Fact]
    public async Task OnGetNotesAsync_IsNotFound_WhenThereIsNoSuchProfile()
    {
        var (pageModel, _, _) = CreatePage(null);

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetNotesAsync_IsNotFound_WhenTheProfileHasNoVersion()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile() with { CurrentProfileVersionId = Guid.Empty });

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetNotesAsync_IsForbidden_WhenCdcApiRefusesTheProfile()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), [Linked("Paper")], throwOnGetManageProfile: Refused());
        WithTempData(pageModel);

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnGetNotesAsync_IsABadGateway_WhenTheProfileCannotBeLoaded()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), throwOnGetMetadata: new HttpRequestException("boom"));

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        AssertStatus(StatusCodes.Status502BadGateway, result);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetNotesAsync_IsABadGateway_AndLogsTheType_WhenCdcApiCannotBeReached(Type exceptionType)
    {
        var (pageModel, _, log) = CreatePage(DraftProfile(), throwOnGetNotes: (Exception)Activator.CreateInstance(exceptionType)!);

        var result = await pageModel.OnGetNotesAsync(NoteTypeId, CancellationToken.None);

        AssertStatus(StatusCodes.Status502BadGateway, result);
        Assert.Contains(log.Messages, message => message.Contains(NoteTypeId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnPostSaveAsync_AddsANewReference_LinkedToTheQuestionAndTheOthersPicked()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput("<p>New reference</p>", ThirdQuestionId);

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var response = SaveResponse(result);
        Assert.True(response.Succeeded);
        Assert.Equal(QuestionReferencesModel.SavedMessage, response.Message);
        Assert.Empty(response.Errors);
        Assert.Equal(ProfileVersionId, api.ChangedNotesProfileVersionId);
        Assert.Equal(NoteTypeId, api.ChangedNotesNoteTypeId);
        var insert = Assert.Single(api.SavedNoteInserts!);
        Assert.Equal("<p>New reference</p>", insert.NoteText);
        AssertQuestions([QuestionId, ThirdQuestionId], insert.QuestionReferenceAdds);
        AssertSection(insert.QuestionReferenceAdds);
        Assert.Empty(api.SavedNoteUpdates!);
    }

    [Fact]
    public async Task OnPostSaveAsync_AddsTheReferenceToTheQuestionOnly_WhenNoOthersArePicked()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput();

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        var insert = Assert.Single(api.SavedNoteInserts!);
        AssertQuestions([QuestionId], insert.QuestionReferenceAdds);
    }

    [Fact]
    public async Task OnPostSaveAsync_DropsScriptsAndUnsafeMarkupFromANewReference()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput("<p onclick=\"x()\">Paper</p><script>alert(1)</script>");

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal("<p>Paper</p>", Assert.Single(api.SavedNoteInserts!).NoteText);
    }

    [Fact]
    public async Task OnPostSaveAsync_LinksNoQuestionTwice_AndNoneOutsideTheSection()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput("Paper", QuestionId, QuestionId, SecondQuestionId, Guid.NewGuid());

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        var insert = Assert.Single(api.SavedNoteInserts!);
        AssertQuestions([QuestionId, SecondQuestionId], insert.QuestionReferenceAdds);
    }

    [Fact]
    public async Task OnPostSaveAsync_LinksAnExistingReference_ToTheQuestionAndTheOthersPicked()
    {
        var existing = Linked("Existing paper", SecondQuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [existing]);
        pageModel.Input = SelectInput(existing.Id, SecondQuestionId, ThirdQuestionId);

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.True(SaveResponse(result).Succeeded);
        Assert.Empty(api.SavedNoteInserts!);
        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.Equal(existing.Id, update.Id);
        Assert.Equal(RowVersion, update.LastUpdated);
        AssertQuestions([QuestionId, ThirdQuestionId], update.QuestionReferenceAdds);
        Assert.Empty(update.QuestionReferenceRemoves);
    }

    // As the legacy dialog did, a question the reference had and the user left unticked loses it.
    [Fact]
    public async Task OnPostSaveAsync_UnlinksTheQuestionsOfAnExistingReferenceThatAreNotPicked()
    {
        var existing = Linked("Existing paper", SecondQuestionId, ThirdQuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [existing]);
        pageModel.Input = SelectInput(existing.Id, ThirdQuestionId);

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        var update = Assert.Single(api.SavedNoteUpdates!);
        AssertQuestions([QuestionId], update.QuestionReferenceAdds);
        AssertQuestions([SecondQuestionId], update.QuestionReferenceRemoves);
    }

    [Fact]
    public async Task OnPostSaveAsync_SavesNothing_WhenTheExistingReferenceAlreadyHasThoseLinks()
    {
        var existing = Linked("Existing paper", QuestionId, SecondQuestionId);
        var (pageModel, api, _) = CreatePage(DraftProfile(), [existing]);
        pageModel.Input = SelectInput(existing.Id, SecondQuestionId);

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.True(SaveResponse(result).Succeeded);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_LeavesTheLinksOfAnExistingReferenceInOtherSectionsAlone()
    {
        var existing = Note("Existing paper", Reference(Guid.NewGuid(), EpidemiologySectionId));
        var (pageModel, api, _) = CreatePage(DraftProfile(), [existing]);
        pageModel.Input = SelectInput(existing.Id);

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        var update = Assert.Single(api.SavedNoteUpdates!);
        Assert.Empty(update.QuestionReferenceRemoves);
    }

    [Fact]
    public async Task OnPostSaveAsync_Fails_WhenTheChosenReferenceIsNoLongerThere()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), [Linked("Another paper")]);
        pageModel.Input = SelectInput(Guid.NewGuid());

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var response = SaveResponse(result);
        Assert.False(response.Succeeded);
        Assert.Equal([QuestionReferencesModel.SaveFailedMessage + QuestionReferencesModel.NoteNotFoundReason], response.Errors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AQID")]
    public async Task OnPostSaveAsync_Fails_WhenTheRowVersionOfTheChosenReferenceIsNotOne(string lastUpdated)
    {
        var existing = Linked("Existing paper");
        var (pageModel, api, _) = CreatePage(DraftProfile(), [existing]);
        pageModel.Input = SelectInput(existing.Id);
        pageModel.Input.LastUpdated = lastUpdated;

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var response = SaveResponse(result);
        Assert.False(response.Succeeded);
        Assert.Equal([QuestionReferencesModel.SaveFailedMessage + QuestionReferencesModel.NoteNotFoundReason], response.Errors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("other")]
    public async Task OnPostSaveAsync_AsksForAnAction_WhenNoneWasChosen(string? mode)
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput();
        pageModel.Input.Mode = mode;
        pageModel.Input.NoteTypeId = null;

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var response = SaveResponse(result);
        Assert.False(response.Succeeded);
        Assert.Equal([QuestionReferencesModel.ActionRequiredMessage], response.Errors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData(QuestionReferencesModel.AddMode)]
    [InlineData(QuestionReferencesModel.SelectMode)]
    public async Task OnPostSaveAsync_AsksForATypeOfReference_WhenNoneWasChosen(string mode)
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.Input = mode == QuestionReferencesModel.AddMode ? AddInput() : SelectInput(Guid.NewGuid());
        pageModel.Input.NoteTypeId = Guid.Empty;

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Contains(QuestionReferencesModel.NoteTypeRequiredMessage, SaveResponse(result).Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    [InlineData("<script>alert(1)</script>")]
    public async Task OnPostSaveAsync_AsksForTheText_WhenANewReferenceHasNone(string noteText)
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput(noteText);

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([QuestionReferencesModel.NoteTextRequiredMessage], SaveResponse(result).Errors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_DoesNotAskForTheText_WhenNoTypeWasChosenYet()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput(string.Empty);
        pageModel.Input.NoteTypeId = null;

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([QuestionReferencesModel.NoteTypeRequiredMessage], SaveResponse(result).Errors);
    }

    [Fact]
    public async Task OnPostSaveAsync_AsksForAReference_WhenNoneWasSelected()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile(), [Linked("Existing paper")]);
        pageModel.Input = SelectInput(Guid.NewGuid());
        pageModel.Input.NoteId = null;

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([QuestionReferencesModel.NoteRequiredMessage], SaveResponse(result).Errors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_AsksForTheTypeAndTheReference_WhenASelectionHasNeither()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());
        pageModel.Input = SelectInput(Guid.NewGuid());
        pageModel.Input.NoteTypeId = null;
        pageModel.Input.NoteId = null;

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([QuestionReferencesModel.NoteTypeRequiredMessage, QuestionReferencesModel.NoteRequiredMessage], SaveResponse(result).Errors);
    }

    [Fact]
    public async Task OnPostSaveAsync_Fails_WhenTheQuestionIsNotInTheSection()
    {
        var (pageModel, api, _) = CreatePage(DraftProfile());
        pageModel.Input = AddInput();
        pageModel.Input.QuestionId = Guid.NewGuid();

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var response = SaveResponse(result);
        Assert.False(response.Succeeded);
        Assert.Equal([QuestionReferencesModel.SaveFailedMessage + QuestionReferencesModel.QuestionNotFoundReason], response.Errors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Theory]
    [InlineData(ProfileNoteChangeOutcome.ValidationFailed, "The text is too long.", true)]
    [InlineData(ProfileNoteChangeOutcome.Conflict, "Someone else changed it.", true)]
    [InlineData(ProfileNoteChangeOutcome.Error, "We could not save the change. Try again later.", false)]
    public async Task OnPostSaveAsync_ReportsWhyCdcApiDidNotSaveTheReference(ProfileNoteChangeOutcome outcome, string reason, bool isLogged)
    {
        var (pageModel, _, log) = CreatePage(DraftProfile(), changeResult: new ProfileNoteChangeResult { Outcome = outcome, ErrorMessage = reason });
        pageModel.Input = AddInput();

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var response = SaveResponse(result);
        Assert.False(response.Succeeded);
        Assert.Null(response.Message);
        Assert.Equal([QuestionReferencesModel.SaveFailedMessage + reason], response.Errors);
        Assert.Equal(isLogged, log.Messages.Any(message => message.Contains(outcome.ToString(), StringComparison.Ordinal)));
    }

    [Fact]
    public async Task OnPostSaveAsync_GivesAGenericReason_WhenCdcApiDoesNotSayWhyItDidNotSave()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile(), changeResult: new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Error });
        pageModel.Input = AddInput();

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([QuestionReferencesModel.SaveFailedMessage + QuestionReferencesModel.RequestFailedReason], SaveResponse(result).Errors);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnPostSaveAsync_ReportsAFailure_AndLogsIt_WhenCdcApiCannotBeReached(Type exceptionType)
    {
        var (pageModel, _, log) = CreatePage(DraftProfile(), throwOnChange: (Exception)Activator.CreateInstance(exceptionType)!);
        pageModel.Input = AddInput();

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var response = SaveResponse(result);
        Assert.False(response.Succeeded);
        Assert.Equal([QuestionReferencesModel.SaveFailedMessage + QuestionReferencesModel.RequestFailedReason], response.Errors);
        Assert.Contains(log.Messages, message => message.Contains(NoteTypeId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnPostSaveAsync_ReportsAFailure_WhenTheExistingReferencesCannotBeRead()
    {
        var (pageModel, api, log) = CreatePage(DraftProfile(), throwOnGetNotes: new HttpRequestException("boom"));
        pageModel.Input = SelectInput(Guid.NewGuid());

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.Equal([QuestionReferencesModel.SaveFailedMessage + QuestionReferencesModel.RequestFailedReason], SaveResponse(result).Errors);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
        Assert.NotEmpty(log.Messages);
    }

    [Fact]
    public async Task OnPostSaveAsync_IsForbidden_ToAUserWhoCannotEditTheSection_AndSavesNothing()
    {
        var (pageModel, api, _) = CreatePage(ReadOnlyDraftProfile());
        pageModel.Input = AddInput();

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        AssertStatus(StatusCodes.Status403Forbidden, result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public async Task OnPostSaveAsync_IsNotFound_WhenThereIsNoSuchProfile()
    {
        var (pageModel, api, _) = CreatePage(null);
        pageModel.Input = AddInput();

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, api.UpdateProfileNotesCallCount);
    }

    [Fact]
    public void ActiveTab_IsTheQuestionsTab()
    {
        var (pageModel, _, _) = CreatePage(DraftProfile());

        Assert.Equal(ProfileSectionTab.Questions, pageModel.ActiveTab);
    }
}
