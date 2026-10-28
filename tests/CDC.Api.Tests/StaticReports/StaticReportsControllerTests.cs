using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public sealed class StaticReportsControllerTests
{
    private static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IStaticReportService> service = new(MockBehavior.Strict);

    private StaticReportsController CreateController(TimeProvider? timeProvider = null) => new(service.Object, timeProvider)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetCurrentStaticReports_ReturnsOk()
    {
        IReadOnlyList<StaticReportDto> reports = [];

        service
            .Setup(svc => svc.GetCurrentStaticReportsAsync(true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        var response = await CreateController().GetCurrentStaticReports(true, false, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(reports);
    }

    [Fact]
    public async Task GetStaticReportHistory_ReturnsOk()
    {
        IReadOnlyList<StaticReportDto> versions = [];

        service
            .Setup(svc => svc.GetStaticReportHistoryAsync(StaticReportId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var response = await CreateController().GetStaticReportHistory(StaticReportId, true, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(versions);
    }

    [Fact]
    public async Task GetStaticReportDocument_ReturnsThePdfFileInline_WhenDocumentExists()
    {
        var document = new StaticReportDataDto { Title = "Help using D2R2", PdfData = [1, 2, 3] };

        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var controller = CreateController();
        var response = await controller.GetStaticReportDocument(VersionId, cancellationToken: CancellationToken.None);

        var fileResult = response.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileContents.Should().BeSameAs(document.PdfData);
        fileResult.ContentType.Should().Be("application/pdf");
        controller.Response.Headers.ContentDisposition.ToString().Should().Be("inline; filename=\"Help using D2R2.pdf\"");
    }

    [Theory]
    [InlineData(2026, 10, 28, 16, 28, 28, "10_28_2026_4_28_28_PM")]
    [InlineData(2026, 1, 5, 9, 5, 7, "1_5_2026_9_05_07_AM")]
    [InlineData(2026, 12, 1, 0, 0, 0, "12_1_2026_12_00_00_AM")]
    public async Task GetStaticReportDocument_SendsAnAttachmentNamedWithTheDownloadDateAndTime_WhenDownloadRequested(
        int year, int month, int day, int hour, int minute, int second, string expectedTimestamp)
    {
        var document = new StaticReportDataDto { Title = "DiseaseRankingReport", PdfData = [1, 2, 3] };

        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var controller = CreateController(new FixedTimeProvider(new DateTime(year, month, day, hour, minute, second)));
        await controller.GetStaticReportDocument(VersionId, download: true, CancellationToken.None);

        controller.Response.Headers.ContentDisposition.ToString().Should().Be($"attachment; filename=\"DiseaseRankingReport_{expectedTimestamp}.pdf\"");
    }

    [Fact]
    public async Task GetStaticReportDocument_KeepsTheTitleAsTheFileName_WhenShownInline()
    {
        var document = new StaticReportDataDto { Title = "Help using D2R2", PdfData = [1, 2, 3] };

        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var controller = CreateController(new FixedTimeProvider(new DateTime(2026, 10, 28, 16, 28, 28)));
        await controller.GetStaticReportDocument(VersionId, download: false, CancellationToken.None);

        controller.Response.Headers.ContentDisposition.ToString().Should().Be("inline; filename=\"Help using D2R2.pdf\"");
    }

    [Fact]
    public async Task GetStaticReportDocument_ReturnsNotFound_WhenDocumentMissing()
    {
        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaticReportDataDto?)null);

        var response = await CreateController().GetStaticReportDocument(VersionId, cancellationToken: CancellationToken.None);

        response.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetStaticReportPublicAccess_ReturnsNoContent()
    {
        service
            .Setup(svc => svc.SetStaticReportPublicAccessAsync(VersionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await CreateController().SetStaticReportPublicAccess(VersionId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteStaticReportVersion_ReturnsNoContent_WhenDeleted()
    {
        service
            .Setup(svc => svc.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteStaticReportVersionOutcome.Success);

        var response = await CreateController().DeleteStaticReportVersion(VersionId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteStaticReportVersion_ReturnsNotFound_WhenVersionDoesNotExist()
    {
        service
            .Setup(svc => svc.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteStaticReportVersionOutcome.NotFound);

        var response = await CreateController().DeleteStaticReportVersion(VersionId, CancellationToken.None);

        response.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteStaticReportVersion_ReturnsForbidden_WhenUserCannotDelete()
    {
        service
            .Setup(svc => svc.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteStaticReportVersionOutcome.Forbidden);

        var response = await CreateController().DeleteStaticReportVersion(VersionId, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void GetUploadPermission_ReturnsOk()
    {
        service.Setup(svc => svc.CanUploadStaticReports).Returns(true);

        var response = CreateController().GetUploadPermission();

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeOfType<StaticReportUploadPermissionDto>().Which.CanUpload.Should().BeTrue();
    }

    [Fact]
    public async Task UploadStaticReport_ReturnsNoContent_OnSuccess()
    {
        var request = new UploadStaticReportRequestDto { Title = "Manual", PdfData = [1, 2, 3], IsUserManual = true, IsPublic = false };

        service
            .Setup(svc => svc.UploadStaticReportAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadStaticReportResult(UploadStaticReportOutcome.Success, null));

        var response = await CreateController().UploadStaticReport(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UploadStaticReport_ReturnsForbidden_WhenUserCannotUpload()
    {
        var request = new UploadStaticReportRequestDto { Title = "Manual", PdfData = [1, 2, 3], IsUserManual = true, IsPublic = false };

        service
            .Setup(svc => svc.UploadStaticReportAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadStaticReportResult(UploadStaticReportOutcome.Forbidden, "denied"));

        var response = await CreateController().UploadStaticReport(request, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task UploadStaticReport_ReturnsBadRequest_OnValidationFailure()
    {
        var request = new UploadStaticReportRequestDto { Title = string.Empty, PdfData = [], IsUserManual = true, IsPublic = false };

        service
            .Setup(svc => svc.UploadStaticReportAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadStaticReportResult(UploadStaticReportOutcome.ValidationFailed, "Please choose a file to upload."));

        var response = await CreateController().UploadStaticReport(request, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(localNow, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
