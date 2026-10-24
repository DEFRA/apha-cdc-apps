using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class EditUserGuidanceModelTests
{
    private static readonly Guid FirstSectionId = Guid.NewGuid();
    private static readonly Guid SecondSectionId = Guid.NewGuid();
    private static readonly Guid FirstQuestionId = Guid.NewGuid();
    private static readonly Guid SecondQuestionId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task OnGetAsync_ListsTheSectionsInOrder_WithTheLegacyNumberedText()
    {
        var model = CreatePage();

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(["1: Disease & overview", "2: Epidemiology"], model.Sections.Select(section => section.Text));
        Assert.False(model.HasSection);
        Assert.Empty(model.Questions);
        Assert.False(model.IsEditing);
        Assert.True(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnGetAsync_ListsTheQuestionsOfTheSelectedSection_InQuestionOrder()
    {
        var sections = new FakeProfileSectionsApiService(metadata: Metadata(), questionInfos: Questions());
        var model = CreatePage(sections: sections);
        model.Section = SecondSectionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(SecondSectionId, sections.RequestedQuestionInfoSectionId);
        Assert.Equal([1, 2], model.Questions.Select(question => question.QuestionNumber));
        Assert.True(model.HasSection);
        Assert.False(model.IsEditing);
    }

    [Fact]
    public async Task OnGetAsync_TreatsAnUnknownSectionAsNoneSelected()
    {
        var sections = new FakeProfileSectionsApiService(metadata: Metadata(), questionInfos: Questions());
        var model = CreatePage(sections: sections);
        model.Section = Guid.NewGuid();
        model.QuestionId = FirstQuestionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Null(model.Section);
        Assert.Null(model.QuestionId);
        Assert.Null(sections.RequestedQuestionInfoSectionId);
        Assert.Empty(model.Questions);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsAnError_WhenTheSectionsCannotBeLoaded(Type exceptionType)
    {
        var logger = new AlwaysEnabledLogger<EditUserGuidanceModel>();
        var sections = new FakeProfileSectionsApiService(throwOnGetProfileQuestionnaireMetadata: (Exception)Activator.CreateInstance(exceptionType)!);
        var model = CreatePage(sections: sections, logger: logger);

        await model.OnGetAsync(CancellationToken.None);

        Assert.Empty(model.Sections);
        Assert.Equal([EditUserGuidanceModel.SectionsLoadFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Contains(logger.Messages, message => message.Contains("profile sections", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenTheQuestionsCannotBeLoaded()
    {
        var sections = new FakeProfileSectionsApiService(metadata: Metadata(), throwOnGetProfileQuestionInfoList: new HttpRequestException("boom"));
        var model = CreatePage(sections: sections);
        model.Section = FirstSectionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Empty(model.Questions);
        Assert.Equal([EditUserGuidanceModel.QuestionsLoadFailureMessage], ErrorsFor(model, string.Empty));
    }

    [Fact]
    public async Task OnGetAsync_OpensTheQuestion_WithItsTextAndRowVersion_AndLineBreaksShownAsBreakTags()
    {
        var question = new ProfileQuestionEditDto
        {
            Id = FirstQuestionId,
            Name = "Is <i>Brucella</i> present?",
            NonTechnicalName = "Is it here?",
            QuestionNumber = 1,
            UserGuidance = "First line\r\nSecond line",
            LastUpdated = RowVersion
        };
        var model = CreatePage(questions: new FakeProfileQuestionsApiService(question));
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.True(model.IsEditing);
        Assert.Equal(1, model.EditingQuestionNumber);
        Assert.Equal("Is <i>Brucella</i> present?", model.Input.Name);
        Assert.Equal("Is it here?", model.Input.NonTechnicalName);
        Assert.Equal("First line<br/>Second line", model.Input.UserGuidance);
        Assert.Equal(Convert.ToBase64String(RowVersion), model.Input.RowVersion);
        Assert.True(model.ModelState.IsValid);
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenTheQuestionIsNotInTheSelectedSection()
    {
        var model = CreatePage(questions: new FakeProfileQuestionsApiService(new ProfileQuestionEditDto { Id = Guid.NewGuid() }));
        model.Section = SecondSectionId;
        model.QuestionId = Guid.NewGuid();

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.IsEditing);
        Assert.Equal([EditUserGuidanceModel.QuestionNotFoundMessage], ErrorsFor(model, string.Empty));
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenTheApiHasNoSuchQuestion()
    {
        var model = CreatePage(questions: new FakeProfileQuestionsApiService(question: null));
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.IsEditing);
        Assert.Equal([EditUserGuidanceModel.QuestionNotFoundMessage], ErrorsFor(model, string.Empty));
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenTheQuestionCannotBeLoaded()
    {
        var logger = new AlwaysEnabledLogger<EditUserGuidanceModel>();
        var model = CreatePage(questions: new FakeProfileQuestionsApiService(throwOnGet: new HttpRequestException("boom")), logger: logger);
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.IsEditing);
        Assert.Equal([EditUserGuidanceModel.QuestionLoadFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Contains(logger.Messages, message => message.Contains(FirstQuestionId.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnPostAsync_ReportsEveryMissingField_AndDoesNotSave()
    {
        var questions = new FakeProfileQuestionsApiService();
        var model = CreatePage(questions: questions);
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;
        model.Input = new EditUserGuidanceInput { Name = "<p><br></p>", NonTechnicalName = " ", UserGuidance = "<p></p>" };

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal([EditUserGuidanceModel.NameRequiredMessage], ErrorsFor(model, EditUserGuidanceModel.NameKey));
        Assert.Equal([EditUserGuidanceModel.NonTechnicalNameRequiredMessage], ErrorsFor(model, EditUserGuidanceModel.NonTechnicalNameKey));
        Assert.Equal([EditUserGuidanceModel.GuidanceRequiredMessage], ErrorsFor(model, EditUserGuidanceModel.UserGuidanceKey));
        Assert.True(model.IsEditing);
        Assert.Equal(1, model.EditingQuestionNumber);
        Assert.Equal(0, questions.UpdateCallCount);
    }

    [Fact]
    public async Task OnPostAsync_RefusesTitlesLongerThan255Characters_NotCountingMarkup()
    {
        var questions = new FakeProfileQuestionsApiService();
        var model = CreatePage(questions: questions);
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;
        model.Input = new EditUserGuidanceInput
        {
            Name = "<i>" + new string('a', 256) + "</i>",
            NonTechnicalName = "<i>" + new string('b', 255) + "</i>",
            UserGuidance = "Guidance"
        };

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal([EditUserGuidanceModel.NameTooLongMessage], ErrorsFor(model, EditUserGuidanceModel.NameKey));
        Assert.Empty(ErrorsFor(model, EditUserGuidanceModel.NonTechnicalNameKey));
        Assert.Equal(0, questions.UpdateCallCount);
    }

    [Fact]
    public async Task OnPostAsync_SavesTheCleanedText_WithTheRowVersionItWasOpenedWith_AndRedirectsToTheSection()
    {
        var questions = new FakeProfileQuestionsApiService();
        var model = CreatePage(questions: questions);
        model.Section = SecondSectionId;
        model.QuestionId = SecondQuestionId;
        model.Input = new EditUserGuidanceInput
        {
            Name = "<p>Is <i>Brucella</i> <b>present</b>?<br></p>\r\n",
            NonTechnicalName = "Is it <em>here</em>?",
            UserGuidance = "<p>Say <b>yes</b> or <i>no</i>.</p><script>alert(1)</script><a href=\"javascript:alert(1)\">bad</a><a href=\"https://example.test\" target=\"_blank\">good</a>",
            RowVersion = Convert.ToBase64String(RowVersion)
        };

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(SecondSectionId, redirect.RouteValues!["section"]);
        Assert.Equal(EditUserGuidanceModel.SaveSucceededMessage, model.SuccessMessage);
        Assert.Null(model.FailureMessage);
        Assert.Equal(SecondQuestionId, questions.UpdatedQuestionId);
        var request = questions.UpdateRequest!;
        Assert.Equal("Is <i>Brucella</i> present?", request.Name);
        Assert.Equal("Is it <em>here</em>?", request.NonTechnicalName);
        Assert.Equal(RowVersion, request.LastUpdated);
        Assert.StartsWith("<p>Say yes or <i>no</i>.</p>", request.UserGuidance, StringComparison.Ordinal);
        Assert.DoesNotContain("script", request.UserGuidance, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", request.UserGuidance, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", request.UserGuidance, StringComparison.Ordinal);
        Assert.Contains("href=\"https://example.test\"", request.UserGuidance, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProfileQuestionUpdateOutcome.Conflict, "The user guidance has been edited by another user")]
    [InlineData(ProfileQuestionUpdateOutcome.Forbidden, "You do not have permission to edit user guidance")]
    [InlineData(ProfileQuestionUpdateOutcome.NotFound, "The question could not be found")]
    [InlineData(ProfileQuestionUpdateOutcome.ValidationFailed, "The title is too long")]
    [InlineData(ProfileQuestionUpdateOutcome.Error, "We could not save the change. Try again later")]
    public async Task OnPostAsync_ReportsWhyTheSaveFailed_AndRedirectsToTheSection(ProfileQuestionUpdateOutcome outcome, string reason)
    {
        var logger = new AlwaysEnabledLogger<EditUserGuidanceModel>();
        var questions = new FakeProfileQuestionsApiService(updateResult: new ProfileQuestionUpdateResult { Outcome = outcome, ErrorMessage = reason });
        var model = CreatePage(questions: questions, logger: logger);
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;
        model.Input = ValidInput();

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(model.SuccessMessage);
        Assert.Equal(EditUserGuidanceModel.SaveFailedPrefix + reason, model.FailureMessage);
        Assert.Contains(logger.Messages, message => message.Contains(outcome.ToString(), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnPostAsync_ReportsAGenericFailure_WhenTheApiCannotBeReached(Type exceptionType)
    {
        var questions = new FakeProfileQuestionsApiService(throwOnUpdate: (Exception)Activator.CreateInstance(exceptionType)!);
        var model = CreatePage(questions: questions);
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;
        model.Input = ValidInput();

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(EditUserGuidanceModel.SaveFailedPrefix + ProfileQuestionsApiService.ErrorMessage, model.FailureMessage);
    }

    [Fact]
    public async Task OnPostAsync_SendsAnEmptyRowVersion_WhenTheOneSubmittedIsNotBase64()
    {
        var questions = new FakeProfileQuestionsApiService();
        var model = CreatePage(questions: questions);
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;
        model.Input = ValidInput();
        model.Input.RowVersion = "not base 64!";

        await model.OnPostAsync(CancellationToken.None);

        Assert.Empty(questions.UpdateRequest!.LastUpdated);
    }

    [Fact]
    public async Task OnPostAsync_RefusesAQuestionThatIsNotInTheSelectedSection()
    {
        var questions = new FakeProfileQuestionsApiService();
        var model = CreatePage(questions: questions);
        model.Section = SecondSectionId;
        model.QuestionId = Guid.NewGuid();
        model.Input = ValidInput();

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal([EditUserGuidanceModel.QuestionNotFoundMessage], ErrorsFor(model, string.Empty));
        Assert.Equal(0, questions.UpdateCallCount);
    }

    [Fact]
    public async Task OnPostAsync_ShowsAnError_WhenTheQuestionsCannotBeLoaded()
    {
        var sections = new FakeProfileSectionsApiService(metadata: Metadata(), throwOnGetProfileQuestionInfoList: new HttpRequestException("boom"));
        var questions = new FakeProfileQuestionsApiService();
        var model = CreatePage(sections: sections, questions: questions);
        model.Section = SecondSectionId;
        model.QuestionId = FirstQuestionId;
        model.Input = ValidInput();

        var result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal([EditUserGuidanceModel.QuestionsLoadFailureMessage], ErrorsFor(model, string.Empty));
        Assert.Equal(0, questions.UpdateCallCount);
    }

    private static EditUserGuidanceInput ValidInput() => new()
    {
        Name = "Question",
        NonTechnicalName = "Plain question",
        UserGuidance = "Guidance",
        RowVersion = Convert.ToBase64String(RowVersion)
    };

    private static List<string> ErrorsFor(EditUserGuidanceModel model, string key) =>
        model.ModelState.TryGetValue(key, out var entry) ? entry.Errors.Select(error => error.ErrorMessage).ToList() : [];

    private static ProfileQuestionnaireMetadataDto Metadata() => new()
    {
        // Listed out of order to show the page sorts them.
        Sections =
        [
            new ProfileSectionMetadataDto { Id = SecondSectionId, ShortName = "EPIDEMIOLOGY", SectionNumber = 2 },
            new ProfileSectionMetadataDto { Id = FirstSectionId, ShortName = "DISEASE AND OVERVIEW", SectionNumber = 1 }
        ]
    };

    private static IReadOnlyList<ProfileQuestionInfoDto> Questions() =>
    [
        new ProfileQuestionInfoDto { Id = SecondQuestionId, Name = "Second", QuestionNumber = 2 },
        new ProfileQuestionInfoDto { Id = FirstQuestionId, Name = "First", QuestionNumber = 1 }
    ];

    private static EditUserGuidanceModel CreatePage(
        FakeProfileSectionsApiService? sections = null,
        FakeProfileQuestionsApiService? questions = null,
        AlwaysEnabledLogger<EditUserGuidanceModel>? logger = null) =>
        new(
            sections ?? new FakeProfileSectionsApiService(metadata: Metadata(), questionInfos: Questions()),
            questions ?? new FakeProfileQuestionsApiService(),
            logger ?? new AlwaysEnabledLogger<EditUserGuidanceModel>());
}
