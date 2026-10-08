using CDC.Api.Application;
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

    private StaticReportService CreateService(bool isProfileEditor = true, bool isUserManagementSystem = false) =>
        new(repository.Object, new FakeUserContext(isProfileEditor, isUserManagementSystem), NullLogger<StaticReportService>.Instance);

    private sealed class FakeUserContext(bool isProfileEditor, bool isUserManagementSystem) : IUserContext
    {
        public bool IsProfileEditor => isProfileEditor;
        public bool IsUserManagementSystem => isUserManagementSystem;
        public bool IsPolicyProfileUser => false;
    }

    private static StaticReportDto ReportDto(string title, int versionMajor, DateTime? effectiveDateTo = null) => new()
    {
        Id = Guid.NewGuid(),
        StaticReportId = StaticReportId,
        Title = title,
        VersionMajor = versionMajor,
        EffectiveDateFrom = DateTime.UtcNow,
        EffectiveDateTo = effectiveDateTo,
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
    public async Task GetCurrentStaticReportsAsync_SetsCanDelete_ForCurrentVersionsWhenUserIsProfileEditor()
    {
        repository
            .Setup(repo => repo.GetCurrentStaticReportsAsync(false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ReportDto("Report", 1)]);

        var reports = await CreateService(isProfileEditor: true).GetCurrentStaticReportsAsync(false, false, CancellationToken.None);

        reports.Should().ContainSingle().Which.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentStaticReportsAsync_CanDeleteIsFalse_WhenUserIsNotProfileEditor()
    {
        repository
            .Setup(repo => repo.GetCurrentStaticReportsAsync(false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ReportDto("Report", 1)]);

        var reports = await CreateService(isProfileEditor: false).GetCurrentStaticReportsAsync(false, false, CancellationToken.None);

        reports.Should().ContainSingle().Which.CanDelete.Should().BeFalse();
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
    public async Task DeleteStaticReportVersionAsync_ReturnsNotFound_WhenDocumentDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaticReportDataDto?)null);

        var outcome = await CreateService().DeleteStaticReportVersionAsync(VersionId, CancellationToken.None);

        outcome.Should().Be(DeleteStaticReportVersionOutcome.NotFound);
        repository.Verify(repo => repo.DeleteStaticReportVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_DeletesAndReturnsSuccess_WhenDocumentExists()
    {
        var dto = new StaticReportDataDto { Title = "Help using D2R2", PdfData = [1, 2, 3] };

        repository
            .Setup(repo => repo.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        repository
            .Setup(repo => repo.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var outcome = await CreateService().DeleteStaticReportVersionAsync(VersionId, CancellationToken.None);

        outcome.Should().Be(DeleteStaticReportVersionOutcome.Success);
        repository.Verify(repo => repo.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_ReturnsForbidden_WhenUserIsNotProfileEditor()
    {
        var dto = new StaticReportDataDto { Title = "Help using D2R2", PdfData = [1, 2, 3] };

        repository
            .Setup(repo => repo.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var outcome = await CreateService(isProfileEditor: false).DeleteStaticReportVersionAsync(VersionId, CancellationToken.None);

        outcome.Should().Be(DeleteStaticReportVersionOutcome.Forbidden);
        repository.Verify(repo => repo.DeleteStaticReportVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanUploadStaticReports_IsTrue_ForProfileEditor() =>
        CreateService(isProfileEditor: true, isUserManagementSystem: false).CanUploadStaticReports.Should().BeTrue();

    [Fact]
    public async Task CanUploadStaticReports_IsFalse_ForUserManagementSystem() =>
        CreateService(isProfileEditor: true, isUserManagementSystem: true).CanUploadStaticReports.Should().BeFalse();

    [Fact]
    public async Task UploadStaticReportAsync_ReturnsForbidden_WhenUserCannotUpload()
    {
        var request = new UploadStaticReportRequestDto { Title = "Manual", PdfData = [1, 2, 3], IsUserManual = true, IsPublic = false };

        var result = await CreateService(isProfileEditor: false).UploadStaticReportAsync(request, CancellationToken.None);

        result.Outcome.Should().Be(UploadStaticReportOutcome.Forbidden);
    }

    [Fact]
    public async Task UploadStaticReportAsync_ReturnsValidationFailed_WhenNoFileSupplied()
    {
        var request = new UploadStaticReportRequestDto { Title = "Manual", PdfData = [], IsUserManual = true, IsPublic = false };

        var result = await CreateService().UploadStaticReportAsync(request, CancellationToken.None);

        result.Outcome.Should().Be(UploadStaticReportOutcome.ValidationFailed);
    }

    [Fact]
    public async Task UploadStaticReportAsync_ReturnsValidationFailed_WhenTitleIsEmpty()
    {
        var request = new UploadStaticReportRequestDto { Title = "   ", PdfData = [1, 2, 3], IsUserManual = true, IsPublic = false };

        var result = await CreateService().UploadStaticReportAsync(request, CancellationToken.None);

        result.Outcome.Should().Be(UploadStaticReportOutcome.ValidationFailed);
        result.ErrorMessage.Should().Be("Please choose a file to upload.");
    }

    [Fact]
    public async Task UploadStaticReportAsync_UploadsAndReturnsSuccess()
    {
        repository
            .Setup(repo => repo.UploadStaticReportAsync("Manual", It.IsAny<byte[]>(), true, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UploadStaticReportRequestDto { Title = "Manual", PdfData = [1, 2, 3], IsUserManual = true, IsPublic = false };

        var result = await CreateService().UploadStaticReportAsync(request, CancellationToken.None);

        result.Outcome.Should().Be(UploadStaticReportOutcome.Success);
        repository.Verify(repo => repo.UploadStaticReportAsync("Manual", It.IsAny<byte[]>(), true, false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
