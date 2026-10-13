using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ReviewCommentApiServiceTests
{
    private sealed class CapturingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Url { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Url = request.RequestUri?.PathAndQuery;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode) { Content = new StringContent("{}") };
        }
    }

    private static ProfileSectionsApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    [Fact]
    public async Task AddProfileReviewCommentAsync_PostsTheComment()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var parentId = Guid.NewGuid();

        var result = await CreateService(handler).AddProfileReviewCommentAsync(
            Guid.NewGuid(), Guid.NewGuid(), parentId, "Subject", "Ref", "<p>Text</p>");

        Assert.Equal(ReviewCommentOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/profile-sections/review-comments", handler.Url);
        Assert.Contains($"\"parentId\":\"{parentId}\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"subject\":\"Subject\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateProfileReviewCommentAsync_PutsTheCommentWithItsRowVersion()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var commentId = Guid.NewGuid();

        var result = await CreateService(handler).UpdateProfileReviewCommentAsync(
            Guid.NewGuid(), Guid.NewGuid(), commentId, new ReviewCommentContent("Subject", "Ref", "Text"), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(ReviewCommentOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Put, handler.Method);
        Assert.Equal("/api/profile-sections/review-comments", handler.Url);
        Assert.Contains($"\"id\":\"{commentId}\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"lastUpdated\":\"AQIDBAUGBwg=\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteProfileReviewCommentAsync_DeletesTheCommentById()
    {
        var handler = new CapturingHandler(HttpStatusCode.NoContent);
        var commentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        var result = await CreateService(handler).DeleteProfileReviewCommentAsync(versionId, sectionId, commentId);

        Assert.Equal(ReviewCommentOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Delete, handler.Method);
        Assert.Equal($"/api/profile-sections/review-comments/{commentId}?profileVersionId={versionId}&profileSectionId={sectionId}", handler.Url);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ReviewCommentOutcome.ValidationFailed)]
    [InlineData(HttpStatusCode.Forbidden, ReviewCommentOutcome.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, ReviewCommentOutcome.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ReviewCommentOutcome.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError, ReviewCommentOutcome.Error)]
    public async Task ReviewCommentCalls_ClassifyFailureStatusCodes(HttpStatusCode statusCode, ReviewCommentOutcome expected)
    {
        var service = CreateService(new CapturingHandler(statusCode));

        var add = await service.AddProfileReviewCommentAsync(Guid.NewGuid(), Guid.NewGuid(), null, "S", "R", "T");
        var update = await service.UpdateProfileReviewCommentAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new ReviewCommentContent("S", "R", "T"), [1, 2, 3, 4, 5, 6, 7, 8]);
        var delete = await service.DeleteProfileReviewCommentAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.All([add, update, delete], result =>
        {
            Assert.Equal(expected, result.Outcome);
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        });
    }
}

public class ReviewCommentHtmlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_ReturnsEmpty_ForBlankInput(string? html) =>
        Assert.Equal(string.Empty, ReviewCommentHtml.Sanitize(html));

    [Fact]
    public void Sanitize_KeepsBasicFormattingAndSafeLinks() =>
        Assert.Equal(
            "<p><b>Bold</b> <a href=\"https://example.com/\" title=\"t\">link</a></p>",
            ReviewCommentHtml.Sanitize("<p><b>Bold</b> <a href=\"https://example.com/\" title=\"t\">link</a></p>"));

    [Theory]
    [InlineData("<script>alert(1)</script>ok", "script")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript")]
    [InlineData("<p style=\"color:red\" onclick=\"x()\">x</p>", "onclick")]
    [InlineData("<iframe src=\"https://example.com\"></iframe>", "iframe")]
    public void Sanitize_RemovesDangerousMarkup(string html, string forbidden) =>
        Assert.DoesNotContain(forbidden, ReviewCommentHtml.Sanitize(html), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void ToDisplayHtml_TurnsPlainTextLineBreaksIntoBreakTags() =>
        Assert.Equal("one<br/>two<br/>three", ReviewCommentHtml.ToDisplayHtml("one\r\ntwo\nthree"));
}
