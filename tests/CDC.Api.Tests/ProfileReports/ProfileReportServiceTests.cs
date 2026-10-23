using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileSections.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportServiceTests
{
    private readonly Mock<IProfileReportRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IProfileSectionRepository> sectionRepository = new(MockBehavior.Strict);
    private readonly Mock<IUserContext> userContext = new();

    public ProfileReportServiceTests()
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(true);
    }

    private CDC.Api.Features.ProfileReports.ProfileReportService CreateService() =>
        new(repository.Object, sectionRepository.Object, userContext.Object, NullLogger<CDC.Api.Features.ProfileReports.ProfileReportService>.Instance);

    [Fact]
    public async Task GetProfileVersionReports_MapsEachReport()
    {
        repository
            .Setup(repo => repo.GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProfileReportTestData.ProfileVersionReport()]);

        var result = await CreateService().GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, false, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].DisplayName.Should().Be("Full Profile Report");
    }

    [Fact]
    public async Task GetProfileReportData_ReturnsNull_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.GetProfileReportDataAsync(
                ProfileReportTestData.ProfileVersionId,
                ProfileReportTestData.ProfileReportId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileReportData?)null);

        var result = await CreateService().GetProfileReportDataAsync(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileReportId,
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileReportData_MapsReportData()
    {
        repository
            .Setup(repo => repo.GetProfileReportDataAsync(
                ProfileReportTestData.ProfileVersionId,
                ProfileReportTestData.ProfileReportId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileReportData { ReportData = ProfileReportTestData.ReportBytes });

        var result = await CreateService().GetProfileReportDataAsync(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileReportId,
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.ReportData.Should().Equal(ProfileReportTestData.ReportBytes);
    }

    [Fact]
    public async Task CreateProfileReport_ReturnsReportId()
    {
        var command = CreateCommand();
        GivenHeader(isPublished: false, effectiveDateTo: null);

        repository
            .Setup(repo => repo.CreateProfileReportAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileReportTestData.ProfileReportId);

        var result = await CreateService().CreateProfileReportAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProfileReportId.Should().Be(ProfileReportTestData.ProfileReportId);
    }

    [Fact]
    public async Task CreateProfileReport_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(ProfileReportTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var result = await CreateService().CreateProfileReportAsync(CreateCommand(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Theory]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, true, true, false, false)]
    [InlineData(false, false, true, false, true)]
    public async Task CreateProfileReport_ReturnsForbidden_WhenTheUserMayNotCreateReports(
        bool isPublished,
        bool isSuperseded,
        bool isEditor,
        bool isPolicyUser,
        bool isUserManagementSystem)
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(isEditor);
        userContext.SetupGet(context => context.IsPolicyProfileUser).Returns(isPolicyUser);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(isUserManagementSystem);
        GivenHeader(isPublished, isSuperseded ? new DateTime(2026, 10, 1) : null);

        var result = await CreateService().CreateProfileReportAsync(CreateCommand(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task GetProfileReportsOverview_ReturnsNotFound_WhenTheVersionDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(ProfileReportTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionHeader?)null);

        var result = await CreateService().GetProfileReportsOverviewAsync(ProfileReportTestData.ProfileVersionId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetProfileReportsOverview_ListsTheReportsWithoutTheFullProfile_AndTheVersionDetails()
    {
        GivenHeader(isPublished: false, effectiveDateTo: null);
        repository
            .Setup(repo => repo.GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                ProfileReportTestData.ProfileVersionReport(),
                ProfileReportTestData.ProfileVersionReport() with { Id = Guid.NewGuid(), ReportName = "SummaryProfileGUID", DisplayName = "Summary profile", HasPdfData = false, FileSize = 0 }
            ]);

        var result = await CreateService().GetProfileReportsOverviewAsync(ProfileReportTestData.ProfileVersionId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullTitle.Should().Be("Avian influenza (What if)");
        result.Value.VersionLabel.Should().Be("2.1");
        result.Value.IsPublished.Should().BeFalse();
        result.Value.IsCurrent.Should().BeTrue();
        result.Value.CanCreateReports.Should().BeTrue();
        var report = result.Value.Reports.Should().ContainSingle().Subject;
        report.ReportName.Should().Be("SummaryProfileGUID");
        report.HasPdfData.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false, true, false, false, true)]
    [InlineData(false, false, false, true, false, true)]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(false, true, true, false, false, false)]
    [InlineData(false, false, true, false, true, false)]
    [InlineData(true, false, false, true, false, false)]
    public async Task GetProfileReportsOverview_AppliesTheLegacyCanCreateRule(
        bool isPublished,
        bool isSuperseded,
        bool isEditor,
        bool isPolicyUser,
        bool isUserManagementSystem,
        bool expected)
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(isEditor);
        userContext.SetupGet(context => context.IsPolicyProfileUser).Returns(isPolicyUser);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(isUserManagementSystem);
        GivenHeader(isPublished, isSuperseded ? new DateTime(2026, 10, 1) : null);
        repository
            .Setup(repo => repo.GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().GetProfileReportsOverviewAsync(ProfileReportTestData.ProfileVersionId, CancellationToken.None);

        result.Value.CanCreateReports.Should().Be(expected);
    }

    private static CreateProfileReportCommand CreateCommand() => new()
    {
        ProfileVersionId = ProfileReportTestData.ProfileVersionId,
        ProfileReportId = ProfileReportTestData.ProfileReportId,
        ReportName = "FullProfileGUID",
        ReportData = ProfileReportTestData.ReportBytes
    };

    private void GivenHeader(bool isPublished, DateTime? effectiveDateTo) =>
        repository
            .Setup(repo => repo.GetProfileVersionHeaderAsync(ProfileReportTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionHeader
            {
                Id = ProfileReportTestData.ProfileVersionId,
                ProfileId = ProfileReportTestData.ProfileId,
                Title = "Avian influenza",
                ScenarioTitle = "What if",
                VersionMajor = 2,
                VersionMinor = 1,
                IsPublished = isPublished,
                EffectiveDateFrom = new DateTime(2026, 1, 1),
                EffectiveDateTo = effectiveDateTo
            });

    private static readonly Guid FirstVersionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SecondVersionId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid FirstSectionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid SecondSectionId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private void GivenDraftHistoryAndSections()
    {
        repository
            .Setup(repo => repo.GetDraftProfileVersionsAsync(ProfileReportTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DraftProfileVersion
                {
                    Id = FirstVersionId,
                    Title = "Avian influenza",
                    ScenarioTitle = "What if",
                    VersionMajor = 2,
                    VersionMinor = 1,
                    EffectiveDateFrom = new DateTime(2026, 9, 11),
                    ProfileStatusName = "Active"
                },
                new DraftProfileVersion
                {
                    Id = SecondVersionId,
                    Title = "Avian influenza",
                    VersionMajor = 2,
                    VersionMinor = 0,
                    EffectiveDateFrom = new DateTime(2026, 1, 1),
                    EffectiveDateTo = new DateTime(2026, 9, 11)
                }
            ]);

        sectionRepository
            .Setup(repo => repo.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileQuestionnaireMetadata
            {
                Sections =
                [
                    new ProfileSectionMetadata { Id = SecondSectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 2, Questions = [] },
                    new ProfileSectionMetadata { Id = FirstSectionId, Name = "Summary", ShortName = "Summ", SectionNumber = 1, Questions = [] }
                ]
            });
    }

    [Fact]
    public async Task GetContributionsReport_BuildsEveryDraftVersionWithEverySection_InNumberOrder()
    {
        GivenDraftHistoryAndSections();
        sectionRepository
            .Setup(repo => repo.GetProfileVersionSectionContributionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        sectionRepository
            .Setup(repo => repo.GetProfileVersionSectionContributionsAsync(FirstVersionId, FirstSectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProfileSectionContribution
                {
                    UserId = Guid.NewGuid(),
                    UserName = "jdoe",
                    FullName = "Jane Doe",
                    OrganisationName = "APHA",
                    LastContributionDate = new DateTime(2026, 9, 12)
                }
            ]);

        var result = await CreateService().GetContributionsReportAsync(ProfileReportTestData.ProfileId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.ProfileId.Should().Be(ProfileReportTestData.ProfileId);
        report.ProfileTitle.Should().Be("Avian influenza (What if)");
        report.Versions.Should().HaveCount(2);

        var first = report.Versions[0];
        first.FullTitle.Should().Be("Avian influenza (What if)");
        first.VersionMajor.Should().Be(2);
        first.VersionMinor.Should().Be(1);
        first.IsCurrent.Should().BeTrue();
        first.ProfileStatusName.Should().Be("Active");
        first.Sections.Select(section => section.SectionNumber).Should().Equal(1, 2);
        first.Sections[0].SectionName.Should().Be("Summary");
        var contribution = first.Sections[0].Contributions.Should().ContainSingle().Subject;
        contribution.FullName.Should().Be("Jane Doe");
        contribution.OrganisationName.Should().Be("APHA");
        first.Sections[1].Contributions.Should().BeEmpty();

        var second = report.Versions[1];
        second.IsCurrent.Should().BeFalse();
        second.EffectiveDateTo.Should().Be(new DateTime(2026, 9, 11));
        second.Sections.Should().OnlyContain(section => section.Contributions.Count == 0);
    }

    [Fact]
    public async Task GetContributionsReport_ReturnsNotFound_WhenTheProfileHasNoDraftVersions()
    {
        repository
            .Setup(repo => repo.GetDraftProfileVersionsAsync(ProfileReportTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().GetContributionsReportAsync(ProfileReportTestData.ProfileId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task GetContributionsReport_IsForbidden_UnlessEditorOrPolicyUserWhoIsNotUserManagement(bool isEditor, bool isPolicyUser, bool isUserManagement)
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(isEditor);
        userContext.SetupGet(context => context.IsPolicyProfileUser).Returns(isPolicyUser);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(isUserManagement);

        var result = await CreateService().GetContributionsReportAsync(ProfileReportTestData.ProfileId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(repo => repo.GetDraftProfileVersionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetContributionsReport_AllowsAPolicyProfileUser()
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(false);
        userContext.SetupGet(context => context.IsPolicyProfileUser).Returns(true);
        repository
            .Setup(repo => repo.GetDraftProfileVersionsAsync(ProfileReportTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().GetContributionsReportAsync(ProfileReportTestData.ProfileId, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetDiseaseReviewsReport_MapsEveryDueSection_PassingTheFiltersThrough()
    {
        var authorId = Guid.NewGuid();
        var fromDate = new DateTime(2027, 1, 1);
        var toDate = new DateTime(2027, 12, 31);
        repository
            .Setup(repo => repo.GetDiseaseReviewsAsync(fromDate, toDate, authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DiseaseReviewSection
                {
                    ProfileVersionId = ProfileReportTestData.ProfileVersionId,
                    Title = "Avian influenza",
                    VersionMajor = 2,
                    VersionMinor = 1,
                    SectionNumber = 3,
                    SectionName = "Surveillance",
                    NextPolicyReview = new DateTime(2027, 2, 3),
                    NextAuthorReview = new DateTime(2027, 4, 5),
                    NextVaReview = new DateTime(2027, 6, 7)
                },
                new DiseaseReviewSection
                {
                    ProfileVersionId = ProfileReportTestData.ProfileVersionId,
                    Title = "Avian influenza",
                    VersionMajor = 2,
                    VersionMinor = 1,
                    SectionNumber = 4,
                    SectionName = "Control"
                }
            ]);

        var result = await CreateService().GetDiseaseReviewsReportAsync(fromDate, toDate, authorId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sections.Should().HaveCount(2);
        var first = result.Value.Sections[0];
        first.ProfileVersionId.Should().Be(ProfileReportTestData.ProfileVersionId);
        first.Title.Should().Be("Avian influenza");
        first.VersionMajor.Should().Be(2);
        first.VersionMinor.Should().Be(1);
        first.SectionNumber.Should().Be(3);
        first.SectionName.Should().Be("Surveillance");
        first.NextPolicyReview.Should().Be(new DateTime(2027, 2, 3));
        first.NextAuthorReview.Should().Be(new DateTime(2027, 4, 5));
        first.NextVaReview.Should().Be(new DateTime(2027, 6, 7));
        var second = result.Value.Sections[1];
        second.NextPolicyReview.Should().BeNull();
        second.NextAuthorReview.Should().BeNull();
        second.NextVaReview.Should().BeNull();
    }

    [Fact]
    public async Task GetDiseaseReviewsReport_ReturnsNoSections_WhenNoReviewIsDue()
    {
        repository
            .Setup(repo => repo.GetDiseaseReviewsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().GetDiseaseReviewsReportAsync(new DateTime(2027, 1, 1), new DateTime(2027, 1, 31), Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sections.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task GetDiseaseReviewsReport_IsForbidden_UnlessAnEditorWhoIsNotUserManagement(bool isEditor, bool isUserManagement)
    {
        userContext.SetupGet(context => context.IsProfileEditor).Returns(isEditor);
        userContext.SetupGet(context => context.IsUserManagementSystem).Returns(isUserManagement);

        var result = await CreateService().GetDiseaseReviewsReportAsync(new DateTime(2027, 1, 1), new DateTime(2027, 1, 31), Guid.NewGuid(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(
            repo => repo.GetDiseaseReviewsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProfilePrintVersion_ReturnsUnavailableDescriptor()
    {
        var result = await CreateService().GetProfilePrintVersionAsync(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileSectionId,
            CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileReportTestData.ProfileVersionId);
        result.ProfileSectionId.Should().Be(ProfileReportTestData.ProfileSectionId);
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetProfileVersionComparisonReport_ReturnsUnavailableDescriptor()
    {
        var result = await CreateService().GetProfileVersionComparisonReportAsync(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileReportId,
            CancellationToken.None);

        result.SourceVersionId.Should().Be(ProfileReportTestData.ProfileVersionId);
        result.TargetVersionId.Should().Be(ProfileReportTestData.ProfileReportId);
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetProfileVersionBespokeReport_ReturnsUnavailableDescriptor()
    {
        var result = await CreateService().GetProfileVersionBespokeReportAsync(
            ProfileReportTestData.ProfileVersionId,
            ["Section1"],
            [],
            [],
            "My Template",
            CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileReportTestData.ProfileVersionId);
        result.Title.Should().Be("My Template");
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetSummaryPrioritisationReport_ReturnsUnavailableDescriptor()
    {
        var result = await CreateService().GetSummaryPrioritisationReportAsync(ProfileReportTestData.ProfileVersionId, CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileReportTestData.ProfileVersionId);
        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetSummaryProfileReport_ReturnsUnavailableDescriptor()
    {
        var result = await CreateService().GetSummaryProfileReportAsync(ProfileReportTestData.ProfileVersionId, CancellationToken.None);

        result.ProfileVersionId.Should().Be(ProfileReportTestData.ProfileVersionId);
        result.IsAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData(ProfileRankingReportType.All)]
    [InlineData(ProfileRankingReportType.Fish)]
    [InlineData(ProfileRankingReportType.Terrestrial)]
    public async Task GetProfileRankingReport_ReturnsUnavailableDescriptor_ForEveryReportType(ProfileRankingReportType reportType)
    {
        var result = await CreateService().GetProfileRankingReportAsync(reportType, "Filter", CancellationToken.None);

        result.ReportType.Should().Be(reportType);
        result.NameOfFilter.Should().Be("Filter");
        result.IsAvailable.Should().BeFalse();
    }
}
