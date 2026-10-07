using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public sealed class StaticReportServiceTests
{
    private static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IStaticReportRepository> repository = new(MockBehavior.Strict);

    private StaticReportService CreateService() =>
        new(repository.Object, NullLogger<StaticReportService>.Instance);

    private static StaticReportDto ReportDto(string title, int versionMajor) => new()
    {
        Id = Guid.NewGuid(),
        StaticReportId = StaticReportId,
        Title = title,
        VersionMajor = versionMajor,
        EffectiveDateFrom = DateTime.UtcNow,
        IsUserManual = false,
        IsPublic = true,
        FileSize = 1024
    };

    [Fact]
    public async Task GetCurrentStaticReportsAsync_OrdersByTitleAscending()
    {
        repository
            .Setup(repo => repo.GetCurrentStaticReportsAsync(false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ReportDto("Zebra report", 1), ReportDto("Apple report", 1)]);

        var reports = await CreateService().GetCurrentStaticReportsAsync(false, false, CancellationToken.None);

        reports.Select(report => report.Title).Should().ContainInOrder("Apple report", "Zebra report");
    }

    [Fact]
    public async Task GetStaticReportHistoryAsync_OrdersByVersionDescending()
    {
        repository
            .Setup(repo => repo.GetStaticReportHistoryAsync(StaticReportId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ReportDto("v1", 1), ReportDto("v2", 2)]);

        var versions = await CreateService().GetStaticReportHistoryAsync(StaticReportId, false, CancellationToken.None);

        versions.Select(version => version.VersionMajor).Should().ContainInOrder(2, 1);
    }

    [Fact]
    public async Task GetStaticReportDataAsync_ReturnsRepositoryResult()
    {
        var dto = new StaticReportDataDto { Title = "Help using D2R2", PdfData = [1, 2, 3] };

        repository
            .Setup(repo => repo.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await CreateService().GetStaticReportDataAsync(VersionId, CancellationToken.None);

        result.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_ReturnsFalse_WhenDocumentDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaticReportDataDto?)null);

        var deleted = await CreateService().DeleteStaticReportVersionAsync(VersionId, CancellationToken.None);

        deleted.Should().BeFalse();
        repository.Verify(repo => repo.DeleteStaticReportVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_DeletesAndReturnsTrue_WhenDocumentExists()
    {
        var dto = new StaticReportDataDto { Title = "Help using D2R2", PdfData = [1, 2, 3] };

        repository
            .Setup(repo => repo.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        repository
            .Setup(repo => repo.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var deleted = await CreateService().DeleteStaticReportVersionAsync(VersionId, CancellationToken.None);

        deleted.Should().BeTrue();
        repository.Verify(repo => repo.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
