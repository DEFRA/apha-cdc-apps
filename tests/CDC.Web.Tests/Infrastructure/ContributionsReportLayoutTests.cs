using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ContributionsReportLayoutTests
{
    private static readonly DateTime From = new(2026, 9, 11);
    private static readonly DateTime To = new(2026, 10, 2);

    [Fact]
    public void Build_ThrowsForNullReport() =>
        Assert.Throws<ArgumentNullException>(() => ContributionsReportLayout.Build(null!));

    [Fact]
    public void Build_UsesPlainTextTitleSectionHeadingsAndDdMmYyyyDates()
    {
        var report = new ContributionsReportDto
        {
            ProfileTitle = "Anthrax <em>(Scenario)</em>",
            Versions =
            [
                new ContributionsReportVersionDto
                {
                    FullTitle = "Anthrax",
                    VersionMajor = 1,
                    VersionMinor = 2,
                    EffectiveDateFrom = From,
                    Sections =
                    [
                        new ContributionsReportSectionDto
                        {
                            SectionNumber = 1,
                            SectionName = "Summary",
                            Contributions =
                            [
                                new ProfileSectionContributionDto { FullName = "Ann Example", OrganisationName = "APHA", LastContributionDate = new DateTime(2026, 1, 5) }
                            ]
                        },
                        new ContributionsReportSectionDto { SectionNumber = 2, SectionName = "Pathogen" }
                    ]
                }
            ]
        };

        var view = ContributionsReportLayout.Build(report);

        Assert.DoesNotContain('<', view.ProfileTitle);
        var version = Assert.Single(view.Versions);
        Assert.Equal("1. Summary", version.Sections[0].Heading);
        Assert.Equal(new ContributionsReportRowView("Ann Example", "APHA", "05/01/2026"), Assert.Single(version.Sections[0].Rows));
        Assert.Equal("2. Pathogen", version.Sections[1].Heading);
        Assert.Empty(version.Sections[1].Rows);
    }

    [Fact]
    public void Build_ShowsOpenEndedValidityAndStatusForTheCurrentVersion()
    {
        var view = ContributionsReportLayout.Build(CreateReport(new ContributionsReportVersionDto
        {
            FullTitle = "Anthrax",
            VersionMajor = 3,
            VersionMinor = 0,
            EffectiveDateFrom = From,
            IsCurrent = true,
            ProfileStatusName = "Active"
        }));

        Assert.Equal("Anthrax - v3.0 (Draft) Valid from 11/09/2026 (Active)", view.Versions[0].Heading);
    }

    [Fact]
    public void Build_ShowsValidityRangeAndOmitsStatus_ForASupersededVersion()
    {
        var view = ContributionsReportLayout.Build(CreateReport(new ContributionsReportVersionDto
        {
            FullTitle = "Anthrax",
            VersionMajor = 1,
            VersionMinor = 0,
            EffectiveDateFrom = From,
            EffectiveDateTo = To,
            IsCurrent = false,
            ProfileStatusName = "Active"
        }));

        Assert.Equal("Anthrax - v1.0 (Draft) Valid from 11/09/2026 to 02/10/2026", view.Versions[0].Heading);
    }

    [Fact]
    public void Build_OmitsTheStatus_WhenTheCurrentVersionHasNone()
    {
        var view = ContributionsReportLayout.Build(CreateReport(new ContributionsReportVersionDto
        {
            FullTitle = "Anthrax",
            VersionMajor = 1,
            VersionMinor = 0,
            EffectiveDateFrom = From,
            IsCurrent = true
        }));

        Assert.Equal("Anthrax - v1.0 (Draft) Valid from 11/09/2026", view.Versions[0].Heading);
    }

    [Fact]
    public void BuildPdfOptions_EncodesTheTitleInTheHeaderAndCarriesTheQualityStatementFooter()
    {
        var options = ContributionsReportLayout.BuildPdfOptions("A & <B>");

        Assert.Contains("A &amp; &lt;B&gt;", options.HeaderHtml);
        Assert.Contains("Contributions report for:", options.HeaderHtml);
        Assert.Contains("pageNumber", options.FooterHtml);
        Assert.Contains("totalPages", options.FooterHtml);
        Assert.Contains(ContributionsReportLayout.QualityStatementText, options.FooterHtml);
    }

    [Theory]
    [InlineData("Anthrax", "Anthrax_Contributions.pdf")]
    [InlineData("Foot/Mouth: A*B", "Foot_Mouth_ A_B_Contributions.pdf")]
    [InlineData("   ", "Profile_Contributions.pdf")]
    public void BuildFileName_ReplacesCharactersThatAreInvalidInFileNames(string title, string expected) =>
        Assert.Equal(expected, ContributionsReportLayout.BuildFileName(title));

    private static ContributionsReportDto CreateReport(ContributionsReportVersionDto version) =>
        new() { ProfileTitle = "Anthrax", Versions = [version] };
}
