using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileReportDocumentsTests
{
    private static readonly IReadOnlyList<ReportReferenceSectionView> NoReferences = [];

    [Fact]
    public void BuildSummaryProfile_RenumbersTheLegacySelectionOfQuestionsUnderTenHeadings()
    {
        var content = ReportContent.Create(
        [
            ReportContent.Section(1, "Description", questions: [ReportContent.Answered(1, "What is it?", "x"), ReportContent.Answered(3, "How big?", "y")]),
            ReportContent.Section(2, "Geography", questions: [ReportContent.YesNo(1, "Is it present?", yes: true), ReportContent.Answered(2, "Where (endemic)?", "e"), ReportContent.Answered(4, "Where (exotic)?", "x"), ReportContent.Answered(7, "Spread?", "s")]),
            ReportContent.Section(9, "Control", questions: [ReportContent.Answered(11, "Compensation?", "c")])
        ]);

        var document = ProfileReportDocuments.BuildSummaryProfile(content);

        Assert.Equal("Summary profile", document.ReportTitle);
        Assert.Equal("1.2", document.VersionLabel);
        Assert.Null(document.PublishedDate);
        Assert.True(document.ShowDraftWatermark);
        Assert.True(document.ShowFrontPage);
        Assert.Equal(
            [
                "1. Description", "2. Rationale for government intervention", "3. Legislation and other mandates", "4. Geographic distribution",
                "5. Risk of introduction/spread", "6. Human health implications", "7. GB disease control strategy", "8. Current surveillance",
                "9. Costs", "10. Compensation"
            ],
            document.Sections.Select(section => section.Heading));
        Assert.All(document.Sections, section => Assert.False(section.StartsOnNewPage));
        Assert.Equal(["1.1 What is it?", "1.2 How big?"], document.Sections[0].Questions.Select(question => question.Heading));
        Assert.Equal(["4.1 Is it present?", "4.2 Spread?"], document.Sections[3].Questions.Select(question => question.Heading));
        Assert.Equal(["10.1 Compensation?"], document.Sections[9].Questions.Select(question => question.Heading));
        Assert.Empty(document.Sections[1].Questions);
    }

    [Theory]
    [InlineData("Yes", "5.1 Where (endemic)?")]
    [InlineData("yes", "5.1 Where (endemic)?")]
    [InlineData("No", "5.1 Where (exotic)?")]
    [InlineData("", "5.1 Where (exotic)?")]
    public void BuildSummaryProfile_ChoosesTheRiskQuestionFromTheAnswerToQuestion2Point1(string answer, string expectedHeading)
    {
        var controlling = ReportContent.Question(
            1,
            "Is it present?",
            groups: ReportContent.Group(fields: ReportContent.Options("Is it?", new ProfileReportOptionDto(answer.Length == 0 ? "Unknown" : answer, answer.Length > 0))));
        var content = ReportContent.Create(
        [
            ReportContent.Section(2, "Geography", questions: [controlling, ReportContent.Answered(2, "Where (endemic)?", "e"), ReportContent.Answered(4, "Where (exotic)?", "x")])
        ]);

        var document = ProfileReportDocuments.BuildSummaryProfile(content);

        Assert.Equal([expectedHeading], document.Sections[4].Questions.Select(question => question.Heading));
    }

    [Fact]
    public void BuildSummaryProfile_ShowsThePublishedDate_ForAPublishedVersion()
    {
        var document = ProfileReportDocuments.BuildSummaryProfile(ReportContent.Create([], isPublished: true, showDraftWatermark: false));

        Assert.Equal("4 March 2026", document.PublishedDate);
        Assert.False(document.ShowDraftWatermark);
    }

    [Fact]
    public void BuildQABrief_UsesPlainLanguageNamesWithTheFullNameUnderneath_AndTheEndemicBranchOfQuestion16()
    {
        var content = ReportContent.Create(
        [
            ReportContent.Section(1, "Description", questions:
            [
                ReportContent.Answered(1, "Technical one", "a", "Plain one"),
                ReportContent.Answered(3, "Technical 1.3", "a", "Species"),
                ReportContent.Answered(4, "Technical 1.4", "a", "Other species")
            ]),
            ReportContent.Section(2, "Geography", questions:
            [
                ReportContent.Question(1, "Present?", "Is it here?", groups: ReportContent.Group(fields: ReportContent.Options("Is it?", new ProfileReportOptionDto("Yes", true), new ProfileReportOptionDto("No", false)))),
                ReportContent.Answered(3, "Exotic 1", "x", "Exotic a"),
                ReportContent.Answered(4, "Exotic 2", "x", "Exotic b"),
                ReportContent.Answered(6, "Endemic 2", "x", "Endemic b")
            ]),
            ReportContent.Section(5, "Welfare", questions: [ReportContent.Answered(8, "Endemic 1", "x", "Endemic a")])
        ]);

        var document = ProfileReportDocuments.BuildQABrief(content);

        Assert.Equal("Question and answer brief", document.ReportTitle);
        var section = Assert.Single(document.Sections);
        Assert.Null(section.Heading);
        var headings = section.Questions.Select(question => question.Heading).ToList();
        Assert.Equal(
            ["1 Plain one", "9 What animals are affected?", "9a Species", "9b Other species", "16 Is it here?", "16.1a Endemic a", "16.1b Endemic b"],
            headings);
        var first = section.Questions[0];
        Assert.Equal("Technical one", first.AdditionalInfo);
        Assert.Equal(0, first.Level);
        var animals = section.Questions[1];
        Assert.Null(animals.Question);
        Assert.Equal(0, animals.Level);
        var species = section.Questions[2];
        Assert.Null(species.AdditionalInfo);
        Assert.Equal(1, species.Level);
        Assert.Equal("Endemic a", section.Questions[5].Heading[6..]);
        Assert.Equal("Technical 1.3", species.Question!.Name);
    }

    [Fact]
    public void BuildQABrief_UsesTheExoticBranchOfQuestion16_WhenTheAnswerIsNo()
    {
        var content = ReportContent.Create(
        [
            ReportContent.Section(2, "Geography", questions:
            [
                ReportContent.Question(1, "Present?", "Is it here?", groups: ReportContent.Group(fields: ReportContent.Options("Is it?", new ProfileReportOptionDto("Yes", false), new ProfileReportOptionDto("No", true)))),
                ReportContent.Answered(3, "Exotic 1", "x", "Exotic a"),
                ReportContent.Answered(4, "Exotic 2", "x", "Exotic b"),
                ReportContent.Answered(6, "Endemic 2", "x", "Endemic b")
            ]),
            ReportContent.Section(5, "Welfare", questions: [ReportContent.Answered(8, "Endemic 1", "x", "Endemic a")])
        ]);

        var headings = ProfileReportDocuments.BuildQABrief(content).Sections[0].Questions.Select(question => question.Heading).ToList();

        Assert.Equal(["9 What animals are affected?", "16 Is it here?", "16.2a Exotic a", "16.2b Exotic b"], headings);
    }

    [Fact]
    public void BuildFullProfile_PrintsEverySectionOnItsOwnPage_WithPlainTextHeadingsAndNotes()
    {
        var content = ReportContent.Create(
        [
            ReportContent.Section(1, "Description <em>one</em>", "A note", ReportContent.Answered(1, "First <b>q</b>", "a"), ReportContent.Answered(2, "Second", "b")),
            ReportContent.Section(2, "Geography")
        ]);

        var document = ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, NoReferences);

        Assert.Equal("Full profile", document.ReportTitle);
        Assert.True(document.ShowFrontPage);
        Assert.Equal(["1. Description one", "2. Geography"], document.Sections.Select(section => section.Heading));
        Assert.Equal(["section-1", "section-2"], document.Sections.Select(section => section.Anchor));
        Assert.All(document.Sections, section => Assert.True(section.StartsOnNewPage));
        Assert.Equal("A note", document.Sections[0].Note);
        Assert.Null(document.Sections[1].Note);
        Assert.Equal(["1.1 First q", "1.2 Second"], document.Sections[0].Questions.Select(question => question.Heading));
        Assert.Same(NoReferences, document.References);
        Assert.Equal("3. Sources of further information and references", document.ReferencesHeading);
    }

    [Fact]
    public void BuildFullProfile_PrintsOneSectionWithoutAFrontPage_WhenASectionIsGiven()
    {
        var content = ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);

        var document = ProfileReportDocuments.BuildFullProfile(content, content.Sections[1].Id, NoReferences);

        Assert.False(document.ShowFrontPage);
        Assert.Equal("2. Two", Assert.Single(document.Sections).Heading);
        Assert.Null(document.ReferencesHeading);
    }

    [Fact]
    public void BuildFullProfile_KeepsTheReferenceHeadingsOfAFullPrint_ButDropsThemForOneSection()
    {
        var content = ReportContent.Create([ReportContent.Section(1, "One"), ReportContent.Section(2, "Two")]);
        IReadOnlyList<ReportReferenceSectionView> references = [new("2. Two", [new ReportReferenceTableView("Scientific paper reference", "- none -", [])])];

        var full = ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, references);
        var single = ProfileReportDocuments.BuildFullProfile(content, content.Sections[1].Id, references);

        Assert.Same(references, full.References);
        Assert.Equal("2. Two", Assert.Single(full.References!).Heading);
        Assert.Null(Assert.Single(single.References!).Heading);
        Assert.Equal("Scientific paper reference", Assert.Single(Assert.Single(single.References!).Tables).Title);
    }

    [Fact]
    public void CreateDocument_TitlesThePdfWithTheLegacyVersionDescription()
    {
        var content = ReportContent.Create([], effectiveDateTo: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var document = ProfileReportDocuments.BuildFullProfile(content, Guid.Empty, NoReferences);

        Assert.Equal("Full profile for Anthrax (What if) - v1.2 (Draft) Valid from 04/03/2026 to 01/04/2026", document.DocumentTitle);
    }

    [Theory]
    [InlineData(false, null, "", "Anthrax (What if) - v1.2 (Draft) Valid from 04/03/2026")]
    [InlineData(false, null, "Validation complete", "Anthrax (What if) - v1.2 (Draft) Valid from 04/03/2026 (Validation complete)")]
    [InlineData(false, "2026-04-01", "Validation complete", "Anthrax (What if) - v1.2 (Draft) Valid from 04/03/2026 to 01/04/2026")]
    [InlineData(false, "2026-04-01", "", "Anthrax (What if) - v1.2 (Draft) Valid from 04/03/2026 to 01/04/2026")]
    [InlineData(true, null, "Active", "Anthrax (What if) - v1.2 (Published) Valid from 04/03/2026")]
    [InlineData(true, "2026-04-01", "Active", "Anthrax (What if) - v1.2 (Published) Valid from 04/03/2026 to 01/04/2026")]
    public void BuildVersionDescription_FollowsTheLegacyProfileVersionToString(bool isPublished, string? effectiveDateTo, string profileStatusName, string expected)
    {
        var content = ReportContent.Create(
            [],
            isPublished,
            effectiveDateTo: effectiveDateTo is null ? null : DateTime.Parse(effectiveDateTo, System.Globalization.CultureInfo.InvariantCulture),
            profileStatusName: profileStatusName);

        Assert.Equal(expected, ProfileReportLayout.BuildVersionDescription(content));
    }

    [Fact]
    public void FirstAnswerText_ReadsTheSelectedOption_TheTextOrNothing()
    {
        Assert.Equal("Yes", ProfileReportLayout.FirstAnswerText(ReportContent.YesNo(1, "q", yes: true)));
        Assert.Equal("typed", ProfileReportLayout.FirstAnswerText(ReportContent.Answered(1, "q", "typed")));
        Assert.Equal(string.Empty, ProfileReportLayout.FirstAnswerText(null));
        Assert.Equal(string.Empty, ProfileReportLayout.FirstAnswerText(ReportContent.Question(1, "q")));
        Assert.Equal(
            string.Empty,
            ProfileReportLayout.FirstAnswerText(ReportContent.Question(1, "q", groups: ReportContent.Group(fields: ReportContent.Options("o", new ProfileReportOptionDto("Yes", false))))));
    }

    [Fact]
    public void BuildPdfOptions_HasTheReportNameAndTitleInTheHeader_AndPageNumbersInTheFooter()
    {
        var document = ProfileReportDocuments.BuildSummaryProfile(ReportContent.Create([]));

        var options = ProfileReportLayout.BuildPdfOptions(document);

        Assert.Contains("Summary profile for:", options.HeaderHtml);
        Assert.Contains("Anthrax (What if)", options.HeaderHtml);
        Assert.DoesNotContain("<em>", options.HeaderHtml);
        Assert.Contains("pageNumber", options.FooterHtml);
        Assert.Contains("totalPages", options.FooterHtml);
        Assert.True(options.GenerateOutline);
        Assert.True(options.PreferCssPageSize);
    }
}
