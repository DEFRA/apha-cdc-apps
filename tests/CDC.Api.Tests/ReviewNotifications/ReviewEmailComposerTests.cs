using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewEmailComposerTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    private static ReviewNotificationsOptions Options(string url = "https://d2r2.example.test/") =>
        new() { InternalHomePageUrl = url, SupportMailbox = "support@example.test" };

    private static ReviewDueUser UserWith(string name, string? scenario, ReviewDueSection section, bool hasPublishedVersion = true) => new(
        Guid.NewGuid(),
        name,
        "reviewer@example.test",
        [new ReviewDueProfile(Guid.NewGuid(), Guid.NewGuid(), "Anthrax", scenario, "2.1", [section], null, hasPublishedVersion)]);

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
        message.Body.Should().Contain("## Anthrax - version 2.1 (draft)")
            .And.Contain("* 3 Transmission - author review 20 October 2026 (due soon)");
    }

    [Fact]
    public void Compose_ListsReviewsInTheLegacyColumnOrder()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active"), new ReviewItem(Today, "Active"), new ReviewItem(Today, "Active")));

        ReviewEmailComposer.Compose(user, Today, Options()).Body.Should().Contain(
            "VA review 20 October 2026 (due soon), policy review 20 October 2026 (due soon), author review 20 October 2026 (due soon)");
    }

    [Fact]
    public void Compose_LeavesGreetingInstructionsAndSupportMailboxToTheTemplate()
    {
        var body = ReviewEmailComposer.Compose(UserWith("Ann", null, Section(new ReviewItem(Today, "Active"))), Today, Options()).Body;

        body.Should().NotContain("Dear").And.NotContain("support@example.test").And.NotContain("Please find");
    }

    [Fact]
    public void Compose_LinksProfileToItsReviewDatesAndComparison()
    {
        var profileId = Guid.NewGuid();
        var user = new ReviewDueUser(Guid.NewGuid(), "Ann", "a@example.test",
            [new ReviewDueProfile(profileId, Guid.NewGuid(), "Anthrax", null, "2.1", [Section(new ReviewItem(Today, "Active"))], null, true)]);

        ReviewEmailComposer.Compose(user, Today, Options()).Body
            .Should().Contain($"(https://d2r2.example.test/SurveillanceProfiles/EditProfileAllRevisionDates/{profileId})")
            .And.Contain($"(https://d2r2.example.test/DiseaseProfiles/CompareVersions?handler=Review&profileId={profileId})");
    }

    [Fact]
    public void Compose_OmitsComparisonLink_WhenTheProfileHasNoPublishedVersion()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")), hasPublishedVersion: false);

        ReviewEmailComposer.Compose(user, Today, Options()).Body.Should().NotContain("CompareVersions");
    }

    [Fact]
    public void Compose_StripsHtmlFromProfileTitles()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")));
        user = user with { Profiles = [user.Profiles[0] with { Title = "<p>Goat Pox</p>" }] };

        ReviewEmailComposer.Compose(user, Today, Options()).Body.Should().Contain("## Goat Pox - version 2.1 (draft)");
    }

    [Fact]
    public void Compose_OrdersProfilesByTheirCleanedTitle()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")));
        var template = user.Profiles[0];
        user = user with
        {
            Profiles = [template with { Title = "<p>Z test clone</p>" }, template with { Title = "<p>Z test</p>" }]
        };

        var body = ReviewEmailComposer.Compose(user, Today, Options()).Body;

        body.IndexOf("## Z test - version", StringComparison.Ordinal)
            .Should().BeLessThan(body.IndexOf("## Z test clone - version", StringComparison.Ordinal));
    }

    [Fact]
    public void Compose_IncludesScenarioTitle_WhenPresent()
    {
        var user = UserWith("Ann", "Scenario A", Section(new ReviewItem(Today, "Active")));

        ReviewEmailComposer.Compose(user, Today, Options()).Body.Should().Contain("Anthrax (Scenario A) - version 2.1 (draft)");
    }

    [Theory]
    [InlineData("Active", -1, "author review 19 October 2026 (overdue)")]
    [InlineData("Proposed", -30, "author review 20 September 2026 (overdue)")]
    [InlineData("Active", 5, "author review 25 October 2026 (due soon)")]
    [InlineData("Proposed", 13, "author review 2 November 2026 (due soon)")]
    [InlineData("Complete", -3, "author review 17 October 2026 (completed)")]
    public void Compose_LabelsEachReviewWithItsStateInText(string status, int daysFromToday, string expected)
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today.AddDays(daysFromToday), status)));

        ReviewEmailComposer.Compose(user, Today, Options()).Body.Should().Contain(expected);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData("Active", 14)]
    [InlineData("Complete", 30)]
    public void Compose_LeavesOutReviews_WithNoStatusOrOutsideTheTwoWeekWindow(string? status, int daysFromToday)
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active"), new ReviewItem(Today.AddDays(daysFromToday), status)));

        ReviewEmailComposer.Compose(user, Today, Options()).Body.Should().NotContain("VA review");
    }

    [Fact]
    public void Compose_LeavesOutReview_WithNoDate()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active"), new ReviewItem(null, "Active")));

        ReviewEmailComposer.Compose(user, Today, Options()).Body.Should().NotContain("VA review");
    }

    [Fact]
    public void Compose_StripsMarkupFromUserSuppliedText()
    {
        var user = UserWith("<b>Ann</b> [x](http://evil.test)", null, Section(new ReviewItem(Today, "Active")));

        var message = ReviewEmailComposer.Compose(user, Today, Options());

        message.ToName.Should().NotContainAny("<", ">", "[", "]");
    }

    [Fact]
    public void Compose_IncludesInternalHomePageLink()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")));

        ReviewEmailComposer.Compose(user, Today, Options()).Body
            .Should().Contain("Internal users can access [D2R2 on the Defra network](https://d2r2.example.test/).");
    }

    [Fact]
    public void Compose_IncludesExternalHomePageLink_WhenConfigured()
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")));
        var options = Options();
        options.ExternalHomePageUrl = "https://external.example.test/";

        ReviewEmailComposer.Compose(user, Today, options).Body
            .Should().Contain("External users can access [D2R2 on the internet](https://external.example.test/).");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    public void Compose_OmitsLinks_WhenUrlMissingOrUnsafe(string url)
    {
        var user = UserWith("Ann", null, Section(new ReviewItem(Today, "Active")));

        ReviewEmailComposer.Compose(user, Today, Options(url)).Body.Should().NotContain("can access").And.NotContain("http");
    }
}
