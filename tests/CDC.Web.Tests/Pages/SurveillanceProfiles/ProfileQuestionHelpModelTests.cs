using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ProfileQuestionHelpModelTests
{
    private static readonly Guid QuestionId = Guid.NewGuid();

    private static ProfileQuestionHelpModel CreatePage(FakeProfileQuestionsApiService questions, AlwaysEnabledLogger<ProfileQuestionHelpModel>? logger = null) =>
        new(questions, logger ?? new AlwaysEnabledLogger<ProfileQuestionHelpModel>());

    private static FakeProfileQuestionsApiService QuestionWithGuidance(string guidance) =>
        new(question: new ProfileQuestionEditDto { Id = QuestionId, Name = "Is <i>Brucella</i> present?", UserGuidance = guidance });

    [Fact]
    public async Task OnGetAsync_ShowsTheQuestionsNameAndItsGuidance()
    {
        var model = CreatePage(QuestionWithGuidance("<p>Say <em>yes</em> if it is.</p>"));

        var result = await model.OnGetAsync(QuestionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Is <i>Brucella</i> present?", model.QuestionName);
        Assert.Equal("<p>Say <em>yes</em> if it is.</p>", model.GuidanceHtml);
        Assert.True(model.HasGuidance);
    }

    // The legacy page put a <br/> in place of each line break of the stored guidance, as the page for editing it does.
    [Fact]
    public async Task OnGetAsync_TurnsTheLineBreaksOfStoredGuidanceIntoLineBreakElements()
    {
        var model = CreatePage(QuestionWithGuidance("First line\r\nSecond line"));

        await model.OnGetAsync(QuestionId, CancellationToken.None);

        Assert.Equal("First line<br/>Second line", model.GuidanceHtml);
    }

    // The guidance is rich text that a person entered, so the page shows only its text formatting and safe links.
    [Fact]
    public async Task OnGetAsync_DropsScriptsAndUnsafeMarkupFromTheGuidance()
    {
        var model = CreatePage(QuestionWithGuidance("<p onclick=\"steal()\">Text</p><script>alert(1)</script><a href=\"javascript:alert(2)\">link</a>"));

        await model.OnGetAsync(QuestionId, CancellationToken.None);

        Assert.DoesNotContain("script", model.GuidanceHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", model.GuidanceHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", model.GuidanceHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Text", model.GuidanceHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p></p>")]
    public async Task OnGetAsync_HasNoGuidance_WhenTheQuestionHasNoneToShow(string guidance)
    {
        var model = CreatePage(QuestionWithGuidance(guidance));

        var result = await model.OnGetAsync(QuestionId, CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.HasGuidance);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenThereIsNoSuchQuestion()
    {
        var model = CreatePage(new FakeProfileQuestionsApiService(question: null));

        var result = await model.OnGetAsync(QuestionId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task OnGetAsync_ReportsABadGateway_AndLogsTheQuestion_WhenCdcApiFails(Type exceptionType)
    {
        var logger = new AlwaysEnabledLogger<ProfileQuestionHelpModel>();
        var model = CreatePage(new FakeProfileQuestionsApiService(throwOnGet: (Exception)Activator.CreateInstance(exceptionType)!), logger);

        var result = await model.OnGetAsync(QuestionId, CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
        Assert.Contains(logger.Messages, message => message.Contains(QuestionId.ToString(), StringComparison.Ordinal));
    }
}
