using System.Security.Claims;
using CDC.Common.Contracts;
using CDC.Web.Authorization;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class CreateBespokeReportModelTests
{
    private static readonly Guid ProfileVersionId = Guid.NewGuid();
    private static readonly ProfileReportQuestionDto Question = ReportContent.Answered(1, "Is it here?", "a");
    private static readonly ProfileReportSectionDto Section = ReportContent.Section(1, "Description", "", Question);
    private static readonly ProfileReportContentDto Content = ReportContent.Create([Section]);
    private static readonly BespokeReportTemplateDto Template = new()
    {
        Id = Guid.NewGuid(),
        Title = "Alpha",
        CheckedList = [BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section), BespokeReportNodes.Value(Question.Id, BespokeNodeType.Question)]
    };

    private static readonly string SectionValue = BespokeReportNodes.Value(Section.Id, BespokeNodeType.Section);
    private static readonly string QuestionValue = BespokeReportNodes.Value(Question.Id, BespokeNodeType.Question);

    /* ---------- GET ---------- */

    [Fact]
    public async Task OnGetAsync_ShowsTheTreeAndTemplates_WithNothingTicked_ForAProfileEditor()
    {
        var templates = new FakeBespokeReportTemplatesApiService { ListResult = new(BespokeTemplatesOutcome.Success, [new BespokeReportTemplateSummaryDto { Id = Template.Id, Title = "Alpha" }]) };
        var model = CreateModel(templates: templates);

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(model.IsProfileEditor);
        Assert.Equal(CreateBespokeReportModel.EditorIntroText, model.IntroText);
        Assert.Equal("Alpha", Assert.Single(model.Templates).Title);
        Assert.Null(model.SelectedTemplate);
        Assert.NotNull(model.Tree);
        Assert.Empty(model.Tree.SelectedValues);
        Assert.Null(model.ErrorMessage);
    }

    [Fact]
    public async Task OnGetAsync_TicksTheSelectedTemplatesNodes()
    {
        var templates = new FakeBespokeReportTemplatesApiService { GetResult = new(BespokeTemplatesOutcome.Success, Template) };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(Template.Id, templates.RequestedTemplateId);
        Assert.Same(Template, model.SelectedTemplate);
        Assert.Contains(SectionValue, model.Tree!.SelectedValues);
        Assert.Contains(QuestionValue, model.Tree.SelectedValues);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheViewerIntroText_ToAPolicyProfileUser()
    {
        var model = CreateModel(role: AuthorizationRoles.PolicyProfileUser);

        await model.OnGetAsync(CancellationToken.None);

        Assert.False(model.IsProfileEditor);
        Assert.Equal(CreateBespokeReportModel.ViewerIntroText, model.IntroText);
    }

    [Fact]
    public async Task OnGetAsync_ShowsNoSelection_WhenTheTemplateWasDeletedInTheMeantime()
    {
        var model = CreateModel();
        model.TemplateId = Guid.NewGuid();

        var result = await model.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(model.SelectedTemplate);
        Assert.Null(model.ErrorMessage);
        Assert.NotNull(model.Tree);
    }

    [Fact]
    public async Task OnGetAsync_IgnoresAnEmptyTemplateId()
    {
        var templates = new FakeBespokeReportTemplatesApiService();
        var model = CreateModel(templates: templates);
        model.TemplateId = Guid.Empty;

        await model.OnGetAsync(CancellationToken.None);

        Assert.Null(templates.RequestedTemplateId);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.NotFound));

        Assert.IsType<NotFoundResult>(await CreateModel(api).OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_Returns403_WhenTheApiRefusesTheUser()
    {
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Forbidden));

        var result = await CreateModel(api).OnGetAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheErrorMessage_WhenTheContentCannotBeLoaded()
    {
        var api = new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Error));
        var model = CreateModel(api);

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.LoadFailureMessage, model.ErrorMessage);
        Assert.Null(model.Tree);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ShowsTheErrorMessage_WhenACallThrows(Type exceptionType)
    {
        var templates = new FakeBespokeReportTemplatesApiService { Throw = (Exception)Activator.CreateInstance(exceptionType)! };
        var model = CreateModel(templates: templates);

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.LoadFailureMessage, model.ErrorMessage);
        Assert.Null(model.Tree);
    }

    [Fact]
    public async Task OnGetAsync_Returns403_WhenTheApiRefusesTheTemplateList()
    {
        var templates = new FakeBespokeReportTemplatesApiService { ListResult = new(BespokeTemplatesOutcome.Forbidden) };

        var result = await CreateModel(templates: templates).OnGetAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task OnGetAsync_ShowsTheErrorMessage_WhenTheTemplateListFails()
    {
        var templates = new FakeBespokeReportTemplatesApiService { ListResult = new(BespokeTemplatesOutcome.Error) };
        var model = CreateModel(templates: templates);

        Assert.IsType<PageResult>(await model.OnGetAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.LoadFailureMessage, model.ErrorMessage);
        Assert.Null(model.Tree);
    }

    [Theory]
    [InlineData(BespokeTemplatesOutcome.Forbidden, true)]
    [InlineData(BespokeTemplatesOutcome.Error, false)]
    public async Task OnGetAsync_HandlesAFailureLoadingTheSelectedTemplate(BespokeTemplatesOutcome outcome, bool expectForbidden)
    {
        var templates = new FakeBespokeReportTemplatesApiService { GetResult = new(outcome) };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;

        var result = await model.OnGetAsync(CancellationToken.None);

        if (expectForbidden)
        {
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        }
        else
        {
            Assert.IsType<PageResult>(result);
            Assert.Equal(CreateBespokeReportModel.LoadFailureMessage, model.ErrorMessage);
        }
    }

    /* ---------- Save template as ---------- */

    [Fact]
    public async Task OnPostSaveAsAsync_SavesTheTrimmedTitleAndTickedNodes_ThenRedirectsToTheNewTemplate()
    {
        var saved = new BespokeReportTemplateDto { Id = Guid.NewGuid(), Title = "My template" };
        var templates = new FakeBespokeReportTemplatesApiService { SaveAsResult = new(BespokeTemplatesOutcome.Success, saved) };
        var model = CreateModel(templates: templates);
        model.NewTemplateTitle = "  My template ";
        model.CheckedNodes = [SectionValue, QuestionValue];

        var result = await model.OnPostSaveAsAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(saved.Id, redirect.RouteValues!["templateId"]);
        Assert.Equal(ProfileVersionId, redirect.RouteValues["profileVersionId"]);
        Assert.Equal(CreateBespokeReportModel.TemplateSavedMessage, model.SuccessMessage);
        Assert.Equal("My template", templates.SavedAs!.Value.Title);
        Assert.Equal([SectionValue, QuestionValue], templates.SavedAs.Value.CheckedList);
    }

    [Theory]
    [InlineData(null, BespokeTemplateTitle.RequiredMessage)]
    [InlineData("   ", BespokeTemplateTitle.RequiredMessage)]
    [InlineData("No-way", BespokeTemplateTitle.CharactersMessage)]
    public async Task OnPostSaveAsAsync_ReopensTheDialogWithTheProblem_WhenTheTitleIsNotAcceptable(string? title, string expected)
    {
        var templates = new FakeBespokeReportTemplatesApiService();
        var model = CreateModel(templates: templates);
        model.NewTemplateTitle = title;
        model.CheckedNodes = [SectionValue];

        var result = await model.OnPostSaveAsAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(expected, model.NewTitleError);
        Assert.Equal(CreateBespokeReportModel.SaveAsDialog, model.OpenDialog);
        Assert.Null(templates.SavedAs);
        Assert.Contains(SectionValue, model.Tree!.SelectedValues);
    }

    [Fact]
    public async Task OnPostSaveAsAsync_ReopensTheDialog_WhenTheTitleIsInUse()
    {
        var templates = new FakeBespokeReportTemplatesApiService { SaveAsResult = new(BespokeTemplatesOutcome.Conflict) };
        var model = CreateModel(templates: templates);
        model.NewTemplateTitle = "Alpha";

        var result = await model.OnPostSaveAsAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(BespokeTemplateTitle.ExistsMessage, model.NewTitleError);
        Assert.Equal(CreateBespokeReportModel.SaveAsDialog, model.OpenDialog);
    }

    [Fact]
    public async Task OnPostSaveAsAsync_ReopensTheDialog_WhenTheApiRejectsTheTitle()
    {
        var templates = new FakeBespokeReportTemplatesApiService { SaveAsResult = new(BespokeTemplatesOutcome.Invalid) };
        var model = CreateModel(templates: templates);
        model.NewTemplateTitle = "Alpha";

        await model.OnPostSaveAsAsync(CancellationToken.None);

        Assert.Equal(BespokeTemplateTitle.CharactersMessage, model.NewTitleError);
    }

    [Fact]
    public async Task OnPostSaveAsAsync_Returns403_ForAUserWhoMayNotEditTemplates()
    {
        var templates = new FakeBespokeReportTemplatesApiService();
        var model = CreateModel(templates: templates, role: AuthorizationRoles.PolicyProfileUser);
        model.NewTemplateTitle = "Alpha";

        var result = await model.OnPostSaveAsAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Null(templates.SavedAs);
    }

    [Fact]
    public async Task OnPostSaveAsAsync_Returns403_WhenTheApiRefusesTheUser()
    {
        var templates = new FakeBespokeReportTemplatesApiService { SaveAsResult = new(BespokeTemplatesOutcome.Forbidden) };
        var model = CreateModel(templates: templates);
        model.NewTemplateTitle = "Alpha";

        var result = await model.OnPostSaveAsAsync(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnPostSaveAsAsync_ShowsTheSaveFailure_WhenTheCallThrows(Type exceptionType)
    {
        var templates = new FakeBespokeReportTemplatesApiService { ThrowOnWrite = (Exception)Activator.CreateInstance(exceptionType)! };
        var model = CreateModel(templates: templates);
        model.NewTemplateTitle = "Alpha";

        Assert.IsType<PageResult>(await model.OnPostSaveAsAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.SaveFailureMessage, model.ErrorMessage);
        Assert.NotNull(model.Tree);
    }

    [Fact]
    public async Task OnPostSaveAsAsync_ShowsTheSaveFailure_WhenTheApiFails()
    {
        var templates = new FakeBespokeReportTemplatesApiService { SaveAsResult = new(BespokeTemplatesOutcome.Error) };
        var model = CreateModel(templates: templates);
        model.NewTemplateTitle = "Alpha";

        Assert.IsType<PageResult>(await model.OnPostSaveAsAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.SaveFailureMessage, model.ErrorMessage);
    }

    /* ---------- Update template ---------- */

    [Fact]
    public async Task OnPostUpdateAsync_ReplacesTheTickedNodes_ThenRedirectsToTheTemplate()
    {
        var templates = new FakeBespokeReportTemplatesApiService { UpdateResult = new(BespokeTemplatesOutcome.Success, Template) };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;
        model.CheckedNodes = [QuestionValue];

        var result = await model.OnPostUpdateAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(Template.Id, redirect.RouteValues!["templateId"]);
        Assert.Equal(CreateBespokeReportModel.TemplateSavedMessage, model.SuccessMessage);
        Assert.Equal(Template.Id, templates.Updated!.Value.Id);
        Assert.Equal([QuestionValue], templates.Updated.Value.CheckedList);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnPostUpdateAsync_DoesNothing_WhenNoTemplateIsSelected(bool emptyGuid)
    {
        var templates = new FakeBespokeReportTemplatesApiService();
        var model = CreateModel(templates: templates);
        model.TemplateId = emptyGuid ? Guid.Empty : null;

        Assert.IsType<PageResult>(await model.OnPostUpdateAsync(CancellationToken.None));
        Assert.Null(templates.Updated);
    }

    [Fact]
    public async Task OnPostUpdateAsync_ExplainsThatTheTemplateIsGone_WhenItWasDeleted()
    {
        var templates = new FakeBespokeReportTemplatesApiService { UpdateResult = new(BespokeTemplatesOutcome.NotFound) };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;

        Assert.IsType<PageResult>(await model.OnPostUpdateAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.TemplateMissingMessage, model.ErrorMessage);
        Assert.Null(model.SelectedTemplate);
    }

    [Fact]
    public async Task OnPostUpdateAsync_Returns403_ForAUserWhoMayNotEditTemplates()
    {
        var model = CreateModel(role: AuthorizationRoles.PolicyProfileUser);
        model.TemplateId = Template.Id;

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await model.OnPostUpdateAsync(CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task OnPostUpdateAsync_ShowsTheSaveFailure_WhenTheApiRejectsIt()
    {
        var templates = new FakeBespokeReportTemplatesApiService { UpdateResult = new(BespokeTemplatesOutcome.Invalid) };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;

        Assert.IsType<PageResult>(await model.OnPostUpdateAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.SaveFailureMessage, model.ErrorMessage);
    }

    /* ---------- Edit title ---------- */

    [Fact]
    public async Task OnPostSaveTitleAsync_RenamesTheTemplate_ThenRedirectsToIt()
    {
        var renamed = new BespokeReportTemplateDto { Id = Template.Id, Title = "Gamma" };
        var templates = new FakeBespokeReportTemplatesApiService { RenameResult = new(BespokeTemplatesOutcome.Success, renamed) };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;
        model.EditedTemplateTitle = " Gamma ";

        var result = await model.OnPostSaveTitleAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(CreateBespokeReportModel.TemplateTitleSavedMessage, model.SuccessMessage);
        Assert.Equal((Template.Id, "Gamma"), templates.Renamed);
    }

    [Fact]
    public async Task OnPostSaveTitleAsync_ReopensTheDialogWithTheProblem_WhenTheTitleIsNotAcceptable()
    {
        var templates = new FakeBespokeReportTemplatesApiService { GetResult = new(BespokeTemplatesOutcome.Success, Template) };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;
        model.EditedTemplateTitle = string.Empty;

        Assert.IsType<PageResult>(await model.OnPostSaveTitleAsync(CancellationToken.None));
        Assert.Equal(BespokeTemplateTitle.RequiredMessage, model.EditTitleError);
        Assert.Equal(CreateBespokeReportModel.EditTitleDialog, model.OpenDialog);
        Assert.Null(templates.Renamed);
    }

    [Fact]
    public async Task OnPostSaveTitleAsync_ReopensTheDialog_WhenTheTitleIsInUse()
    {
        var templates = new FakeBespokeReportTemplatesApiService
        {
            GetResult = new(BespokeTemplatesOutcome.Success, Template),
            RenameResult = new(BespokeTemplatesOutcome.Conflict)
        };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;
        model.EditedTemplateTitle = "Beta";

        Assert.IsType<PageResult>(await model.OnPostSaveTitleAsync(CancellationToken.None));
        Assert.Equal(BespokeTemplateTitle.ExistsMessage, model.EditTitleError);
        Assert.Equal(CreateBespokeReportModel.EditTitleDialog, model.OpenDialog);
    }

    [Fact]
    public async Task OnPostSaveTitleAsync_DoesNothing_WhenNoTemplateIsSelected()
    {
        var templates = new FakeBespokeReportTemplatesApiService();
        var model = CreateModel(templates: templates);
        model.EditedTemplateTitle = "Gamma";

        Assert.IsType<PageResult>(await model.OnPostSaveTitleAsync(CancellationToken.None));
        Assert.Null(templates.Renamed);
    }

    [Fact]
    public async Task OnPostSaveTitleAsync_Returns403_ForAUserWhoMayNotEditTemplates()
    {
        var model = CreateModel(role: AuthorizationRoles.PolicyProfileUser);
        model.TemplateId = Template.Id;

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await model.OnPostSaveTitleAsync(CancellationToken.None)).StatusCode);
    }

    /* ---------- Delete template ---------- */

    [Theory]
    [InlineData(BespokeTemplatesOutcome.Success)]
    [InlineData(BespokeTemplatesOutcome.NotFound)]
    public async Task OnPostDeleteAsync_DeletesTheTemplate_ThenRedirectsWithNoneSelected(BespokeTemplatesOutcome outcome)
    {
        var templates = new FakeBespokeReportTemplatesApiService { DeleteOutcome = outcome };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;

        var result = await model.OnPostDeleteAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.False(redirect.RouteValues!.ContainsKey("templateId"));
        Assert.Equal(CreateBespokeReportModel.TemplateDeletedMessage, model.SuccessMessage);
        Assert.Equal(Template.Id, templates.Deleted);
    }

    [Fact]
    public async Task OnPostDeleteAsync_DoesNothing_WhenNoTemplateIsSelected()
    {
        var templates = new FakeBespokeReportTemplatesApiService();
        var model = CreateModel(templates: templates);

        Assert.IsType<PageResult>(await model.OnPostDeleteAsync(CancellationToken.None));
        Assert.Null(templates.Deleted);
    }

    [Fact]
    public async Task OnPostDeleteAsync_Returns403_WhenTheApiRefusesTheUser_OrTheUserMayNotEdit()
    {
        var refused = CreateModel(templates: new FakeBespokeReportTemplatesApiService { DeleteOutcome = BespokeTemplatesOutcome.Forbidden });
        refused.TemplateId = Template.Id;
        var viewer = CreateModel(role: AuthorizationRoles.PolicyProfileUser);
        viewer.TemplateId = Template.Id;

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await refused.OnPostDeleteAsync(CancellationToken.None)).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(await viewer.OnPostDeleteAsync(CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ShowsTheSaveFailure_WhenTheApiFails()
    {
        var templates = new FakeBespokeReportTemplatesApiService { DeleteOutcome = BespokeTemplatesOutcome.Error };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;

        Assert.IsType<PageResult>(await model.OnPostDeleteAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.SaveFailureMessage, model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_ShowsTheSaveFailure_WhenTheCallThrows()
    {
        var templates = new FakeBespokeReportTemplatesApiService { ThrowOnWrite = new HttpRequestException() };
        var model = CreateModel(templates: templates);
        model.TemplateId = Template.Id;

        Assert.IsType<PageResult>(await model.OnPostDeleteAsync(CancellationToken.None));
        Assert.Equal(CreateBespokeReportModel.SaveFailureMessage, model.ErrorMessage);
    }

    /* ---------- Generate bespoke report ---------- */

    [Fact]
    public async Task OnPostGenerateAsync_DownloadsThePdf_NamedAfterTheSelectedTemplate()
    {
        var builder = new FakeBespokeReportPdfBuilder();
        var templates = new FakeBespokeReportTemplatesApiService { GetResult = new(BespokeTemplatesOutcome.Success, Template) };
        var model = CreateModel(templates: templates, builder: builder);
        model.TemplateId = Template.Id;
        model.CheckedNodes = [SectionValue, QuestionValue, BespokeReportNodes.Value(Question.Id, BespokeNodeType.Guidance)];

        var result = await model.OnPostGenerateAsync(CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("Alpha.pdf", file.FileDownloadName);
        Assert.Equal(ProfileVersionId, builder.ProfileVersionId);
        Assert.Equal("Alpha", builder.TemplateTitle);
        Assert.Equal([Section.Id], builder.Selection!.Sections);
        Assert.Equal([Question.Id], builder.Selection.Questions);
        Assert.Equal([Question.Id], builder.Selection.Guidance);
    }

    [Fact]
    public async Task OnPostGenerateAsync_UsesTheDefaultTitle_WhenNoTemplateIsSelected()
    {
        var builder = new FakeBespokeReportPdfBuilder();
        var model = CreateModel(builder: builder);
        model.CheckedNodes = [SectionValue];

        var file = Assert.IsType<FileContentResult>(await model.OnPostGenerateAsync(CancellationToken.None));

        Assert.Equal("Bespoke Profile Report.pdf", file.FileDownloadName);
        Assert.Null(builder.TemplateTitle);
    }

    [Fact]
    public async Task OnPostGenerateAsync_UsesTheDefaultTitle_WhenTheSelectedTemplateNoLongerExists()
    {
        var builder = new FakeBespokeReportPdfBuilder();
        var model = CreateModel(builder: builder);
        model.TemplateId = Guid.NewGuid();
        model.CheckedNodes = [SectionValue];

        var file = Assert.IsType<FileContentResult>(await model.OnPostGenerateAsync(CancellationToken.None));

        Assert.Equal("Bespoke Profile Report.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task OnPostGenerateAsync_AsksForASelection_WhenNothingIsTicked()
    {
        var builder = new FakeBespokeReportPdfBuilder();
        var model = CreateModel(builder: builder);
        model.CheckedNodes = [QuestionValue, "junk"];

        var result = await model.OnPostGenerateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(CreateBespokeReportModel.NothingSelectedMessage, model.ErrorMessage);
        Assert.Null(builder.Selection);
        Assert.Contains(QuestionValue, model.Tree!.SelectedValues);
    }

    [Theory]
    [InlineData(typeof(PdfRenderException))]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnPostGenerateAsync_ShowsTheFailure_KeepingTheTickedNodes_WhenTheReportCannotBeBuilt(Type exceptionType)
    {
        var exception = exceptionType == typeof(PdfRenderException) ? new PdfRenderException("boom") : (Exception)Activator.CreateInstance(exceptionType)!;
        var model = CreateModel(builder: new FakeBespokeReportPdfBuilder(throwOnBuild: exception));
        model.CheckedNodes = [SectionValue];

        var result = await model.OnPostGenerateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(CreateBespokeReportModel.GenerateFailureMessage, model.ErrorMessage);
        Assert.Contains(SectionValue, model.Tree!.SelectedValues);
    }

    /* ---------- helpers ---------- */

    private static FakeProfileReportsApiService CreateApi() =>
        new(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, Content));

    private static CreateBespokeReportModel CreateModel(
        FakeProfileReportsApiService? api = null,
        IBespokeReportTemplatesApiService? templates = null,
        FakeBespokeReportPdfBuilder? builder = null,
        string role = AuthorizationRoles.ProfileEditor)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"))
        };

        return new CreateBespokeReportModel(
            api ?? CreateApi(),
            templates ?? new FakeBespokeReportTemplatesApiService(),
            builder ?? new FakeBespokeReportPdfBuilder(),
            CreateNavigation(),
            NullLogger<CreateBespokeReportModel>.Instance)
        {
            ProfileVersionId = ProfileVersionId,
            PageContext = new PageContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider())
        };
    }

    private static INavigationAuthorizationService CreateNavigation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCdcWebAuthorization();

        return services.BuildServiceProvider().GetRequiredService<INavigationAuthorizationService>();
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
