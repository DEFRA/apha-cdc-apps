using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportServiceTests
{
    private readonly Mock<IProfileReportRepository> repository = new(MockBehavior.Strict);

    private CDC.Api.Features.ProfileReports.ProfileReportService CreateService() =>
        new(repository.Object, NullLogger<CDC.Api.Features.ProfileReports.ProfileReportService>.Instance);

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
        var command = new CreateProfileReportCommand
        {
            ProfileVersionId = ProfileReportTestData.ProfileVersionId,
            ProfileReportId = ProfileReportTestData.ProfileReportId,
            ReportName = "FullProfileGUID",
            ReportData = ProfileReportTestData.ReportBytes
        };

        repository
            .Setup(repo => repo.CreateProfileReportAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileReportTestData.ProfileReportId);

        var result = await CreateService().CreateProfileReportAsync(command, CancellationToken.None);

        result.ProfileReportId.Should().Be(ProfileReportTestData.ProfileReportId);
    }

    [Fact]
    public async Task GetContributionsReport_ReturnsUnavailableDescriptor()
    {
        var result = await CreateService().GetContributionsReportAsync(ProfileReportTestData.ProfileId, CancellationToken.None);

        result.ProfileId.Should().Be(ProfileReportTestData.ProfileId);
        result.IsAvailable.Should().BeFalse();
        result.Message.Should().NotBeEmpty();
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
