using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewEmailComposerTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    private static ReviewNotificationsOptions Options(string url = "https://d2r2.example.test/") =>
        new() { InternalHomePageUrl = url, SupportMailbox = "support@example.test" };

    private static ReviewDueUser UserWith(string name, string? scenario, ReviewDueSection section) => new(
        Guid.NewGuid(),
        name,
        "reviewer@example.test",
        [new ReviewDueProfile(Guid.NewGuid(), "Anthrax", scenario, "2.1", [section])]);

    private static ReviewDueSection Section(ReviewItem author, ReviewItem? technical = null, ReviewItem? policy = null) =>
        new(3, "Transmission", author, technical ?? new ReviewItem(null, null), policy ?? new ReviewItem(null, null));

    [Fact]
    public void Compose_AddressesTheReviewerAndIdentifiesTheProfile()
    {
        var user = UserWith("Ann Reviewer", null, Section(new ReviewItem(Today, "Active")));

        var message = ReviewEmailComposer.Compose(user, Today, Options());

        message.ToAddress.Should().Be("reviewer@example.test");
        message.ToName.Should().Be("Ann Reviewer");
        message.Subject.Should().Be("D2R2 - Profile review request");
        message.HtmlBody.Should().Contain("Dear Ann Reviewer,")
            .And.Contain("Anthrax - v 2.1 (Draft)")
            .And.Contain("3 Transmission");
    }

    [Fact]
    public void Compose_LinksProfileHeadingToItsReviewPage()
    {
        var profileId = Guid.NewGuid();
        var user = new ReviewDueUser(Guid.NewGuid(), "Ann", "a@example.test",
            [new ReviewDueProfile(profileId, "Anthrax", null, "2.1", [Section(new ReviewItem(Today, "Active"))])]);

        ReviewEmailComposer.Compose(user, Today, Options()).HtmlBody
            .Should().Contain($"href=\"https://d2r2.example.test/SurveillanceProfiles/EditProfileAllRevisionDates/{profileId}\"");
    }

    [Fact]
    public void Compose_IncludesScenarioTitle_WhenPresent()
    {
        var user = UserWith("Ann", "Scenario A", Section(new ReviewItem(Today, "Active")));

        ReviewEmailComposer.Compose(user, Today, Options()).HtmlBody.Should().Contain("Anthrax (Scenario A) - v 2.1 (Draft)");
    }

    [Theory]
    [InlineData("Active", -1, "(Overdue)")]
    [InlineData("Proposed", -1, "(Overdue)")]
    [InlineData("Active", 5, "(Due soon)")]
    [InlineData("Complete", 5, "(Completed)")]
    public void Compose_LabelsReviewStatusAsText(string status, int daysFromToday, string expected)
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today.AddDays(daysFromToday), status)));

        ReviewEmailComposer.Compose(user, Today, Options()).HtmlBody.Should().Contain(expected);
    }

    [Fact]
    public void Compose_ShowsDateOnlyWhenStatusUnspecified()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today.AddDays(2), null)));

        var body = ReviewEmailComposer.Compose(user, Today, Options()).HtmlBody;

        body.Should().Contain("22/10/2026").And.NotContain("(Due soon)");
    }

    [Fact]
    public void Compose_LeavesCellBlank_WhenReviewOutsideWindowOrUnscheduled()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today.AddDays(30), "Active"), new ReviewItem(null, "Active")));

        var body = ReviewEmailComposer.Compose(user, Today, Options()).HtmlBody;

        body.Should().NotContain("(Due soon)").And.NotContain("(Overdue)").And.NotContain("(Completed)");
    }

    [Fact]
    public void Compose_HtmlEncodesUserSuppliedText()
    {
        var user = UserWith("<script>x</script>", null, Section(new ReviewItem(Today, "Active")));

        var body = ReviewEmailComposer.Compose(user, Today, Options()).HtmlBody;

        body.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
    }

    [Fact]
    public void Compose_IncludesHomePageLinkAndSupportMailbox()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")));

        var body = ReviewEmailComposer.Compose(user, Today, Options()).HtmlBody;

        body.Should().Contain("href=\"https://d2r2.example.test/\"").And.Contain("support@example.test");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    public void Compose_OmitsHomePageLink_WhenUrlMissingOrUnsafe(string url)
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")));

        ReviewEmailComposer.Compose(user, Today, Options(url)).HtmlBody.Should().NotContain("href=");
    }
}
