using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public sealed class StaticReportsControllerTests
{
    private static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IStaticReportService> service = new(MockBehavior.Strict);

    private StaticReportsController CreateController() => new(service.Object);

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
    public async Task GetStaticReportDocument_ReturnsOk_WhenDocumentExists()
    {
        var document = new StaticReportDataDto { Title = "Help using D2R2", PdfData = [1, 2, 3] };

        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var response = await CreateController().GetStaticReportDocument(VersionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(document);
    }

    [Fact]
    public async Task GetStaticReportDocument_ReturnsNotFound_WhenDocumentMissing()
    {
        service
            .Setup(svc => svc.GetStaticReportDataAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaticReportDataDto?)null);

        var response = await CreateController().GetStaticReportDocument(VersionId, CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteStaticReportVersion_ReturnsNoContent_WhenDeleted()
    {
        service
            .Setup(svc => svc.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var response = await CreateController().DeleteStaticReportVersion(VersionId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteStaticReportVersion_ReturnsNotFound_WhenVersionDoesNotExist()
    {
        service
            .Setup(svc => svc.DeleteStaticReportVersionAsync(VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await CreateController().DeleteStaticReportVersion(VersionId, CancellationToken.None);

        response.Should().BeOfType<NotFoundResult>();
    }
}
