using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public sealed class StaticReportsControllerTests
{
    private static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IStaticReportService> service = new(MockBehavior.Strict);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private StaticReportsController CreateController() => new(service.Object, new FixedTimeProvider(new DateTimeOffset(2026, 10, 28, 13, 2, 51, TimeSpan.Zero)))
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
        var header = ContentDispositionHeaderValue.Parse(controller.Response.Headers.ContentDisposition.ToString());
        header.DispositionType.ToString().Should().Be("inline");
        header.FileName.ToString().Trim('"').Should().Be("Help_using_D2R2_28_10_2026_13_02_51.pdf");
    }

    [Fact]
    public async Task GetStaticReportDocument_SendsAnAttachment_WhenDownloadRequested()
    {
        var document = new StaticReportDataDto { Title = "D2R2 Quality Statement", PdfData = [1, 2, 3] };

        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var controller = CreateController();
        await controller.GetStaticReportDocument(VersionId, download: true, CancellationToken.None);

        var header = ContentDispositionHeaderValue.Parse(controller.Response.Headers.ContentDisposition.ToString());
        header.DispositionType.ToString().Should().Be("attachment");
        header.FileName.ToString().Trim('"').Should().Be("D2R2_Quality_Statement_28_10_2026_13_02_51.pdf");
    }

    [Theory]
    [InlineData("Dr Smith's guide, v2; final", "Dr_Smiths_guide_v2_final")]
    [InlineData("<b>Q&A</b> sheet", "Q&A_sheet")]
    [InlineData("A/B: C", "A_B__C")]
    public async Task GetStaticReportDocument_AppliesLegacyFileNameSanitisation(string title, string expected)
    {
        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaticReportDataDto { Title = title, PdfData = [1] });

        var controller = CreateController();
        await controller.GetStaticReportDocument(VersionId, download: true, CancellationToken.None);

        var header = ContentDispositionHeaderValue.Parse(controller.Response.Headers.ContentDisposition.ToString());
        header.FileName.ToString().Trim('"').Should().Be($"{expected}_28_10_2026_13_02_51.pdf");
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
}
