using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class DiseaseReviewsReportLayoutTests
{
    private static readonly DateTime From = new(2027, 2, 1);
    private static readonly DateTime To = new(2027, 2, 28);
    private static readonly Guid FirstVersionId = Guid.NewGuid();
    private static readonly Guid SecondVersionId = Guid.NewGuid();

    [Fact]
    public void Build_ThrowsForNullReport() =>
        Assert.Throws<ArgumentNullException>(() => DiseaseReviewsReportLayout.Build(null!, string.Empty, "Jane Doe", From, To));

    [Fact]
    public void Build_StatesWhoseReviewsAreDueBetweenWhichDates_AndKeepsTheDescription()
    {
        var view = DiseaseReviewsReportLayout.Build(new DiseaseReviewsReportDto(), "<i>Quarterly</i> check", "Jane Doe", From, To);

        Assert.Equal("<i>Quarterly</i> check", view.Description);
        Assert.Equal("Jane Doe due between 01-02-2027 and 28-02-2027", view.DateRange);
    }

    [Fact]
    public void Build_ReturnsNoProfiles_WhenNothingIsDue() =>
        Assert.Empty(DiseaseReviewsReportLayout.Build(new DiseaseReviewsReportDto(), string.Empty, "Jane Doe", From, To).Profiles);

    [Fact]
    public void Build_GroupsRowsByProfileVersionInTheOrderEachFirstAppears_AndNumbersSectionsInOrder()
    {
        var report = new DiseaseReviewsReportDto
        {
            Sections =
            [
                Row(SecondVersionId, "Brucellosis", sectionNumber: 2, sectionName: "Control"),
                Row(FirstVersionId, "Anthrax", sectionNumber: 5, sectionName: "Surveillance"),
                Row(SecondVersionId, "Brucellosis", sectionNumber: 1, sectionName: "Summary"),
                Row(FirstVersionId, "Anthrax", sectionNumber: 3, sectionName: "Pathogen")
            ]
        };

        var profiles = DiseaseReviewsReportLayout.Build(report, string.Empty, "Jane Doe", From, To).Profiles;

        Assert.Equal(["Brucellosis v1.2", "Anthrax v1.2"], profiles.Select(profile => profile.Name));
        Assert.Equal(["1: Summary", "2: Control"], profiles[0].Sections.Select(section => section.Section));
        Assert.Equal(["3: Pathogen", "5: Surveillance"], profiles[1].Sections.Select(section => section.Section));
    }

    [Fact]
    public void Build_NamesAProfileWithItsTitleAndVersion_OrWithNothingWhenItHasNoTitle()
    {
        var report = new DiseaseReviewsReportDto
        {
            Sections =
            [
                Row(FirstVersionId, "Avian <em>influenza</em>", sectionNumber: 1, sectionName: "Summary") with { VersionMajor = 12, VersionMinor = 0 },
                Row(SecondVersionId, string.Empty, sectionNumber: 1, sectionName: "Summary")
            ]
        };

        var profiles = DiseaseReviewsReportLayout.Build(report, string.Empty, "Jane Doe", From, To).Profiles;

        Assert.Equal("Avian <em>influenza</em> v12.0", profiles[0].Name);
        Assert.Equal(string.Empty, profiles[1].Name);
    }

    [Fact]
    public void Build_ShowsEachDueDateAsDdMmmYyyy_AndLeavesAnUnsetOneEmpty()
    {
        var report = new DiseaseReviewsReportDto
        {
            Sections =
            [
                Row(FirstVersionId, "Anthrax", sectionNumber: 1, sectionName: "Summary") with
                {
                    NextPolicyReview = new DateTime(2027, 2, 3),
                    NextAuthorReview = new DateTime(2027, 2, 14),
                    NextVaReview = new DateTime(2027, 2, 25)
                },
                Row(FirstVersionId, "Anthrax", sectionNumber: 2, sectionName: "Control") with { NextAuthorReview = new DateTime(2027, 2, 9) }
            ]
        };

        var sections = Assert.Single(DiseaseReviewsReportLayout.Build(report, string.Empty, "Jane Doe", From, To).Profiles).Sections;

        Assert.Equal(new DiseaseReviewsReportSectionView("1: Summary", "03 Feb 2027", "14 Feb 2027", "25 Feb 2027"), sections[0]);
        Assert.Equal(new DiseaseReviewsReportSectionView("2: Control", string.Empty, "09 Feb 2027", string.Empty), sections[1]);
    }

    [Fact]
    public void BuildPdfOptions_PutsTheAuthorAndDatesAboveTheProductionDateAndPageNumbers()
    {
        var options = DiseaseReviewsReportLayout.BuildPdfOptions("A & B", From, To, new DateTimeOffset(2027, 3, 4, 14, 5, 6, TimeSpan.Zero));

        Assert.Null(options.HeaderHtml);
        Assert.Contains("width:100%;box-sizing:border-box;padding:0 12.7mm", options.FooterHtml);
        Assert.Contains("A &amp; B due 01-02-27-28-02-27", options.FooterHtml);
        Assert.Contains("Produced: 04/03/2027. Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span>", options.FooterHtml);
        Assert.True(options.FooterHtml!.IndexOf("due", StringComparison.Ordinal) < options.FooterHtml.IndexOf("Produced", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildPdfOptions_ReducesAnAuthorNameWithMarkupToPlainText()
    {
        var options = DiseaseReviewsReportLayout.BuildPdfOptions("<b>Jane</b> <script>x</script>Doe", From, To, DateTimeOffset.UnixEpoch);

        Assert.DoesNotContain("<b>", options.FooterHtml);
        Assert.DoesNotContain("<script>", options.FooterHtml);
        Assert.Contains("Jane xDoe due", options.FooterHtml);
    }

    [Fact]
    public void BuildFileName_UsesTheLegacyNameAndTimeProduced() =>
        Assert.Equal(
            "DiseaseReviewsReport_04_03_2027_14_05_06.pdf",
            DiseaseReviewsReportLayout.BuildFileName(new DateTimeOffset(2027, 3, 4, 14, 5, 6, TimeSpan.Zero)));

    private static DiseaseReviewsReportRowDto Row(Guid versionId, string title, int sectionNumber, string sectionName) => new()
    {
        ProfileVersionId = versionId,
        Title = title,
        VersionMajor = 1,
        VersionMinor = 2,
        SectionNumber = sectionNumber,
        SectionName = sectionName
    };
}
