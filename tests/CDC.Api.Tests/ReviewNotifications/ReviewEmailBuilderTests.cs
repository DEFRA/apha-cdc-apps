using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.ReviewNotifications;

public class ReviewEmailBuilderTests
{
    private static readonly DateTime Today = new(2026, 10, 20);

    private static ReviewNotificationRecipient Recipient(params ReviewSection[] sections) => new(
        Guid.NewGuid(),
        "Ann <Admin>",
        "ann@example.com",
        [new ReviewProfileVersion(Guid.NewGuid(), "Anthrax", null, "3.2", sections)]);

    private static ReviewSection Section(ReviewSchedule author, ReviewSchedule? technical = null, ReviewSchedule? policy = null) =>
        new(7, "Wider Society", author, technical ?? new ReviewSchedule(null, null), policy ?? new ReviewSchedule(null, null));

    private static string Html(ReviewNotificationRecipient recipient, ReviewNotificationsOptions? options = null) =>
        ReviewEmailBuilder.Build(recipient, options ?? new ReviewNotificationsOptions(), Today).HtmlBody;

    [Fact]
    public void Build_SetsAddressSubjectAndEncodedGreeting()
    {
        var message = ReviewEmailBuilder.Build(Recipient(Section(new ReviewSchedule(null, null))), new ReviewNotificationsOptions(), Today);

        message.ToAddress.Should().Be("ann@example.com");
        message.ToName.Should().Be("Ann <Admin>");
        message.Subject.Should().Be("D2R2 - Profile review request");
        message.HtmlBody.Should().Contain("Dear Ann &lt;Admin&gt;,").And.NotContain("<Admin>");
    }

    [Fact]
    public void Build_ProducesAccessibleTableWithHeaders()
    {
        var html = Html(Recipient(Section(new ReviewSchedule(null, null))));

        html.Should().Contain("<html lang=\"en\">")
            .And.Contain("scope=\"col\"")
            .And.Contain("scope=\"row\"")
            .And.Contain("7. Wider Society")
            .And.Contain("Anthrax - version 3.2 (draft)");
    }

    [Fact]
    public void Build_IncludesScenarioTitle_WhenPresent()
    {
        var recipient = new ReviewNotificationRecipient(
            Guid.NewGuid(),
            "Ann",
            "ann@example.com",
            [new ReviewProfileVersion(Guid.NewGuid(), "Anthrax", "Cattle", "1.0", [Section(new ReviewSchedule(null, null))])]);

        Html(recipient).Should().Contain("Anthrax (Cattle) - version 1.0 (draft)");
    }

    [Theory]
    [InlineData("2026-10-01", "Active", "Overdue")]
    [InlineData("2026-10-01", "Proposed", "Overdue")]
    [InlineData("2026-10-25", "Active", "Due soon")]
    [InlineData("2026-10-20", "Proposed", "Due soon")]
    [InlineData("2026-10-01", "Complete", "Completed")]
    public void Build_LabelsReviewStatusAsText(string date, string status, string expectedLabel)
    {
        var html = Html(Recipient(Section(new ReviewSchedule(DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture), status))));

        html.Should().Contain(expectedLabel);
    }

    [Fact]
    public void Build_FormatsDatesInGovUkStyle()
    {
        var html = Html(Recipient(Section(new ReviewSchedule(new DateTime(2026, 5, 18), "Active"))));

        html.Should().Contain("18 May 2026");
    }

    [Fact]
    public void Build_ShowsNotDueWithoutLabel_WhenDateIsOutsideTheWindowOrStatusUnknown()
    {
        var html = Html(Recipient(Section(
            new ReviewSchedule(new DateTime(2027, 1, 1), "Active"),
            new ReviewSchedule(null, "Active"),
            new ReviewSchedule(new DateTime(2026, 10, 1), null))));

        html.Should().Contain("Not due").And.NotContain("Overdue").And.NotContain("Due soon");
        html.Should().Contain("1 October 2026");
    }

    [Fact]
    public void Build_SkipsProfileVersionsWithoutSections()
    {
        var recipient = new ReviewNotificationRecipient(
            Guid.NewGuid(),
            "Ann",
            "ann@example.com",
            [new ReviewProfileVersion(Guid.NewGuid(), "Empty profile", null, "1.0", [])]);

        Html(recipient).Should().NotContain("Empty profile");
    }

    [Fact]
    public void Build_IncludesHomePageLinkAndSupportMailbox_WhenConfigured()
    {
        var html = Html(
            Recipient(Section(new ReviewSchedule(null, null))),
            new ReviewNotificationsOptions { InternalHomePageUrl = "https://d2r2.example.gov.uk/", SupportMailbox = "support@example.gov.uk" });

        html.Should().Contain("<a href=\"https://d2r2.example.gov.uk/\">").And.Contain("contact support@example.gov.uk.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    public void Build_OmitsHomePageLink_WhenUrlIsMissingOrNotHttp(string? url)
    {
        var html = Html(Recipient(Section(new ReviewSchedule(null, null))), new ReviewNotificationsOptions { InternalHomePageUrl = url });

        html.Should().NotContain("<a href").And.Contain("contact the D2R2 mailbox.");
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("<p>Japanese Encephalitis Virus (JEV)</p>", "Japanese Encephalitis Virus (JEV)")]
    [InlineData("Foot &amp; mouth", "Foot & mouth")]
    public void StripMarkup_RemovesTagsAndDecodesEntities(string? input, string expected) =>
        ReviewEmailBuilder.StripMarkup(input).Should().Be(expected);
}
