using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Commands;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Features.StaticReports.Queries;
using FluentAssertions;
using MediatR;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public class StaticReportHandlerTests
{
    private readonly Mock<IStaticReportRepository> repository = new(MockBehavior.Strict);

    [Fact]
    public async Task GetCurrentStaticReportsQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<Api.Domain.Entities.StaticReportVersion> reports = [StaticReportTestData.StaticReportVersion()];

        repository
            .Setup(repo => repo.GetCurrentAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        var result = await new GetCurrentStaticReportsQueryHandler(repository.Object)
            .Handle(new GetCurrentStaticReportsQuery(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Title.Should().Be("Help using D2R2 guidance");
    }

    [Fact]
    public async Task GetStaticReportHistoryQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<Api.Domain.Entities.StaticReportVersion> versions = [StaticReportTestData.StaticReportVersion()];

        repository
            .Setup(repo => repo.GetHistoryAsync(StaticReportTestData.StaticReportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var result = await new GetStaticReportHistoryQueryHandler(repository.Object)
            .Handle(new GetStaticReportHistoryQuery(StaticReportTestData.StaticReportId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task GetStaticReportDataQueryHandler_ReturnsNotFound_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.GetDataAsync(StaticReportTestData.VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.Domain.Entities.StaticReportData?)null);

        var result = await new GetStaticReportDataQueryHandler(repository.Object)
            .Handle(new GetStaticReportDataQuery(StaticReportTestData.VersionId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetStaticReportDataQueryHandler_ReturnsSuccess()
    {
        var data = new Api.Domain.Entities.StaticReportData
        {
            PdfData = StaticReportTestData.PdfBytes,
            IsPublic = false,
            Title = "Help using D2R2 guidance"
        };

        repository
            .Setup(repo => repo.GetDataAsync(StaticReportTestData.VersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var result = await new GetStaticReportDataQueryHandler(repository.Object)
            .Handle(new GetStaticReportDataQuery(StaticReportTestData.VersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PdfData.Should().Equal(StaticReportTestData.PdfBytes);
    }

    [Fact]
    public async Task UploadStaticReportCommandHandler_ReturnsSuccess()
    {
        repository
            .Setup(repo => repo.UploadAsync("Help using D2R2 guidance", StaticReportTestData.PdfBytes, true, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new UploadStaticReportCommandHandler(repository.Object).Handle(
            new UploadStaticReportCommand("Help using D2R2 guidance", StaticReportTestData.PdfBytes, true, false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task DeleteStaticReportVersionCommandHandler_ReturnsSuccess()
    {
        repository
            .Setup(repo => repo.DeleteAsync(StaticReportTestData.VersionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new DeleteStaticReportVersionCommandHandler(repository.Object).Handle(
            new DeleteStaticReportVersionCommand(StaticReportTestData.VersionId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.DeleteAsync(StaticReportTestData.VersionId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
