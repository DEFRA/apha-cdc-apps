using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class BespokeReportNodesTests
{
    private static readonly Guid Id = Guid.Parse("3f2b1a0c-1234-5678-abcd-ef1234567890");

    [Theory]
    [InlineData(BespokeNodeType.Profile, "3f2b1a0c-1234-5678-abcd-ef12345678900")]
    [InlineData(BespokeNodeType.Section, "3f2b1a0c-1234-5678-abcd-ef12345678901")]
    [InlineData(BespokeNodeType.Question, "3f2b1a0c-1234-5678-abcd-ef12345678902")]
    [InlineData(BespokeNodeType.Guidance, "3f2b1a0c-1234-5678-abcd-ef12345678903")]
    public void Value_IsTheIdentifierFollowedByTheTypeNumber_AsTheLegacyPageStoredIt_AndRoundTrips(BespokeNodeType type, string expected)
    {
        var value = BespokeReportNodes.Value(Id, type);

        Assert.Equal(expected, value);
        Assert.True(BespokeReportNodes.TryParse(value, out var id, out var parsedType));
        Assert.Equal(Id, id);
        Assert.Equal(type, parsedType);
    }

    [Theory]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890Profile", BespokeNodeType.Profile)]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890Section", BespokeNodeType.Section)]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890Question", BespokeNodeType.Question)]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890Guidance", BespokeNodeType.Guidance)]
    public void TryParse_AlsoAcceptsTheTypeAsAName(string value, BespokeNodeType expected)
    {
        Assert.True(BespokeReportNodes.TryParse(value, out var id, out var type));
        Assert.Equal(Id, id);
        Assert.Equal(expected, type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890")]
    [InlineData("not-a-guid-at-all-not-a-guid-at-all!Section")]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890Banana")]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef123456789099")]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890Section,Question")]
    [InlineData("3f2b1a0c-1234-5678-abcd-ef1234567890 Section")]
    public void TryParse_RejectsAMalformedValue(string? value) =>
        Assert.False(BespokeReportNodes.TryParse(value, out _, out _));

    [Fact]
    public void ToSelection_SortsTheTickedNodesByType_AndIgnoresAnythingElse()
    {
        var section = Guid.NewGuid();
        var question = Guid.NewGuid();
        var guidance = Guid.NewGuid();

        var selection = BespokeReportNodes.ToSelection(
        [
            BespokeReportNodes.Value(Guid.NewGuid(), BespokeNodeType.Profile),
            BespokeReportNodes.Value(section, BespokeNodeType.Section),
            BespokeReportNodes.Value(question, BespokeNodeType.Question),
            BespokeReportNodes.Value(guidance, BespokeNodeType.Guidance),
            BespokeReportNodes.Value(question, BespokeNodeType.Guidance),
            "junk",
            string.Empty
        ]);

        Assert.Equal([section], selection.Sections);
        Assert.Equal([question], selection.Questions);
        Assert.Equivalent(new[] { guidance, question }, selection.Guidance);
    }

    [Fact]
    public void ToSelection_ReturnsAnEmptySelection_ForNothing()
    {
        var selection = BespokeReportNodes.ToSelection(null);

        Assert.Empty(selection.Sections);
        Assert.Empty(selection.Questions);
        Assert.Empty(selection.Guidance);
    }
}

public class BespokeTemplateTitleTests
{
    [Theory]
    [InlineData("My template", null)]
    [InlineData("  Padded 2  ", null)]
    [InlineData("", BespokeTemplateTitle.RequiredMessage)]
    [InlineData("   ", BespokeTemplateTitle.RequiredMessage)]
    [InlineData(null, BespokeTemplateTitle.RequiredMessage)]
    [InlineData("Has-punctuation", BespokeTemplateTitle.CharactersMessage)]
    [InlineData("<script>", BespokeTemplateTitle.CharactersMessage)]
    public void Validate_AppliesTheLegacyRules(string? title, string? expected) =>
        Assert.Equal(expected, BespokeTemplateTitle.Validate(title));

    [Fact]
    public void Validate_RejectsATitleOverTheColumnWidth_ButNotOneAtIt()
    {
        Assert.Null(BespokeTemplateTitle.Validate(new string('a', 255)));
        Assert.Equal(BespokeTemplateTitle.TooLongMessage, BespokeTemplateTitle.Validate(new string('a', 256)));
    }
}

public class BespokeReportTreeTests
{
    private static readonly ProfileReportQuestionDto QuestionOne = ReportContent.Answered(1, "<p>Is it <em>here</em>?</p>", "a");
    private static readonly ProfileReportQuestionDto QuestionTwo = ReportContent.Answered(2, "Where is it?", "b");
    private static readonly ProfileReportQuestionDto QuestionThree = ReportContent.Answered(1, "Who is affected?", "c");
    private static readonly ProfileReportSectionDto Description = ReportContent.Section(1, "Description", "", QuestionOne, QuestionTwo);
    private static readonly ProfileReportSectionDto Epidemiology = ReportContent.Section(2, "Epidemiology", "", QuestionThree);
    private static readonly ProfileReportContentDto Content = ReportContent.Create([Description, Epidemiology]);

    [Fact]
    public void Build_LaysOutTheProfile_ItsSections_AndAGuidanceNodeBeforeEachQuestion()
    {
        var tree = BespokeReportTree.Build(Content, []);

        Assert.True(tree.AllowMultipleSelection);
        Assert.True(tree.CascadeSelection);
        Assert.Equal("CheckedNodes", tree.FieldName);
        var root = Assert.Single(tree.Nodes);
        Assert.Equal("Anthrax (What if)", root.Label);
        Assert.True(root.Expanded);
        Assert.Equal(BespokeReportNodes.Value(Content.ProfileVersionId, BespokeNodeType.Profile), root.Value);
        Assert.Equal(["1 : Description", "2 : Epidemiology"], root.Children.Select(node => node.Label));
        Assert.Equal(
            ["1 Guidance : Is it here?", "1 : Is it here?", "2 Guidance : Where is it?", "2 : Where is it?"],
            root.Children[0].Children.Select(node => node.Label));
        Assert.Equal(
            [BespokeReportTree.GuidanceKind, null, BespokeReportTree.GuidanceKind, null],
            root.Children[0].Children.Select(node => node.Kind));
        Assert.Equal(BespokeReportNodes.Value(QuestionOne.Id, BespokeNodeType.Guidance), root.Children[0].Children[0].Value);
        Assert.Equal(BespokeReportNodes.Value(QuestionOne.Id, BespokeNodeType.Question), root.Children[0].Children[1].Value);
        Assert.Empty(tree.SelectedValues);
    }

    [Fact]
    public void Build_TicksTheNodesOfASavedSelection()
    {
        var tree = BespokeReportTree.Build(
            Content,
            [
                BespokeReportNodes.Value(Description.Id, BespokeNodeType.Section),
                BespokeReportNodes.Value(QuestionTwo.Id, BespokeNodeType.Question),
                BespokeReportNodes.Value(QuestionOne.Id, BespokeNodeType.Guidance)
            ]);

        Assert.Equivalent(
            new[]
            {
                BespokeReportNodes.Value(Content.ProfileVersionId, BespokeNodeType.Profile),
                BespokeReportNodes.Value(Description.Id, BespokeNodeType.Section),
                BespokeReportNodes.Value(QuestionTwo.Id, BespokeNodeType.Question),
                BespokeReportNodes.Value(QuestionOne.Id, BespokeNodeType.Guidance)
            },
            tree.SelectedValues);
    }

    [Fact]
    public void Build_TicksTheParentsOfATickedNode_AsTheLegacyPageDid()
    {
        var tree = BespokeReportTree.Build(Content, [BespokeReportNodes.Value(QuestionThree.Id, BespokeNodeType.Question)]);

        Assert.Equivalent(
            new[]
            {
                BespokeReportNodes.Value(Content.ProfileVersionId, BespokeNodeType.Profile),
                BespokeReportNodes.Value(Epidemiology.Id, BespokeNodeType.Section),
                BespokeReportNodes.Value(QuestionThree.Id, BespokeNodeType.Question)
            },
            tree.SelectedValues);
    }

    [Fact]
    public void Build_TicksNodesStoredByTheLegacyPage_WhoseTypeIsANumber_OrAName()
    {
        var tree = BespokeReportTree.Build(Content, [$"{Description.Id}1", $"{QuestionOne.Id}Question"]);

        Assert.Contains(BespokeReportNodes.Value(Description.Id, BespokeNodeType.Section), tree.SelectedValues);
        Assert.Contains(BespokeReportNodes.Value(QuestionOne.Id, BespokeNodeType.Question), tree.SelectedValues);
    }

    [Fact]
    public void Build_IgnoresValuesForNodesThatNoLongerExist()
    {
        var tree = BespokeReportTree.Build(Content, [BespokeReportNodes.Value(Guid.NewGuid(), BespokeNodeType.Section), "junk"]);

        Assert.Empty(tree.SelectedValues);
    }
}

public class ProfileBespokeReportDocumentsTests
{
    private static readonly ProfileReportQuestionDto QuestionOne = ReportContent.Answered(1, "<p>Is it <em>here</em>?</p>", "a");
    private static readonly ProfileReportQuestionDto QuestionTwo = ReportContent.Answered(2, "Where is it?", "b");
    private static readonly ProfileReportQuestionDto QuestionThree = ReportContent.Answered(1, "Who is affected?", "c");
    private static readonly ProfileReportSectionDto Description = ReportContent.Section(1, "Description", "Read this first", QuestionOne, QuestionTwo);
    private static readonly ProfileReportSectionDto Epidemiology = ReportContent.Section(2, "Epidemiology", "", QuestionThree);
    private static readonly ProfileReportContentDto Content = ReportContent.Create([Description, Epidemiology], isPublished: true, showDraftWatermark: false);

    private static BespokeReportSelection Selection(IEnumerable<Guid>? sections = null, IEnumerable<Guid>? questions = null, IEnumerable<Guid>? guidance = null) =>
        new(
            new HashSet<Guid>(sections ?? []),
            new HashSet<Guid>(questions ?? []),
            new HashSet<Guid>(guidance ?? []));

    [Fact]
    public void Build_PrintsOnlyTheTickedSections_EachOnItsOwnPage_UnderTheDefaultTitle()
    {
        var document = ProfileBespokeReportDocuments.Build(Content, Selection(sections: [Epidemiology.Id], questions: [QuestionThree.Id]), new Dictionary<Guid, string>(), templateTitle: null);

        Assert.Equal("Bespoke Profile Report", document.ReportTitle);
        Assert.True(document.ShowFrontPage);
        Assert.Equal("4 March 2026", document.PublishedDate);
        var section = Assert.Single(document.Sections);
        Assert.Equal("2. Epidemiology", section.Heading);
        Assert.True(section.StartsOnNewPage);
        Assert.Null(section.Note);
        Assert.Equal(["2.1 Who is affected?"], section.Questions.Select(question => question.Heading));
        Assert.Null(section.Questions[0].GuidanceHtml);
        Assert.Same(QuestionThree, section.Questions[0].Question);
    }

    [Fact]
    public void Build_PrintsTheSectionNote_AndEachTickedQuestionThenItsGuidanceInQuestionOrder()
    {
        var guidance = new Dictionary<Guid, string> { [QuestionOne.Id] = "<p>Read the <em>manual</em></p>", [QuestionTwo.Id] = "More" };

        var document = ProfileBespokeReportDocuments.Build(
            Content,
            Selection(sections: [Description.Id], questions: [QuestionOne.Id, QuestionTwo.Id], guidance: [QuestionOne.Id, QuestionTwo.Id]),
            guidance,
            templateTitle: "My template");

        Assert.Equal("My template", document.ReportTitle);
        var section = Assert.Single(document.Sections);
        Assert.Equal("Read this first", section.Note);
        Assert.Equal(
            ["1.1 Is it here?", "1.1 : Is it here? (Guidance)", "1.2 Where is it?", "1.2 : Where is it? (Guidance)"],
            section.Questions.Select(question => question.Heading));
        Assert.Equal([null, "<p>Read the <em>manual</em></p>", null, "More"], section.Questions.Select(question => question.GuidanceHtml));
        Assert.Null(section.Questions[1].Question);
    }

    [Fact]
    public void Build_PrintsGuidanceWithoutItsQuestion_AndAnEmptyBodyWhenTheQuestionHasNoGuidance()
    {
        var document = ProfileBespokeReportDocuments.Build(Content, Selection(sections: [Description.Id], guidance: [QuestionTwo.Id]), new Dictionary<Guid, string>(), "  ");

        Assert.Equal("Bespoke Profile Report", document.ReportTitle);
        var question = Assert.Single(Assert.Single(document.Sections).Questions);
        Assert.Equal("1.2 : Where is it? (Guidance)", question.Heading);
        Assert.Equal(string.Empty, question.GuidanceHtml);
    }

    [Fact]
    public void Build_PrintsNoQuestionsOfAnUntickedSection_EvenIfTheyAreTicked()
    {
        var document = ProfileBespokeReportDocuments.Build(Content, Selection(sections: [Description.Id], questions: [QuestionThree.Id]), new Dictionary<Guid, string>(), null);

        Assert.Empty(Assert.Single(document.Sections).Questions);
    }

    [Fact]
    public void GuidanceQuestionIds_AreTheTickedGuidanceOfTickedSections_WithoutDuplicates()
    {
        var ids = ProfileBespokeReportDocuments.GuidanceQuestionIds(
            Content,
            Selection(sections: [Description.Id], guidance: [QuestionOne.Id, QuestionThree.Id, Guid.NewGuid()]));

        Assert.Equal([QuestionOne.Id], ids);
    }

    [Fact]
    public void GuidanceQuestionIds_AreEmpty_WhenNoGuidanceIsTicked()
    {
        Assert.Empty(ProfileBespokeReportDocuments.GuidanceQuestionIds(Content, Selection(sections: [Description.Id], questions: [QuestionOne.Id])));
    }
}
