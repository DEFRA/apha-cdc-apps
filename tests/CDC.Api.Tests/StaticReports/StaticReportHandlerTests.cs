using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.StaticReports.Commands;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Features.StaticReports.Queries;
using FluentAssertions;
using MediatR;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public class StaticReportHandlerTests
{
    private static readonly Guid VersionId = Guid.Parse("c80b8e93-21d2-453a-b0e0-3f522d03971d");
    private static readonly Guid StaticReportId = Guid.Parse("65fe96f2-ea67-4618-9e45-0af0026da1ef");

    private readonly Mock<IStaticReportRepository> repository = new(MockBehavior.Strict);

    [Fact]
    public async Task GetCurrentStaticReportsQueryHandler_MapsEntitiesToDtos()
    {
        IReadOnlyList<StaticReportVersion> versions =
        [
            new StaticReportVersion
            {
                Id = VersionId,
                StaticReportId = StaticReportId,
                Title = "D2R2 Quality Statement",
                VersionMajor = 1,
                EffectiveDateFrom = new DateTime(2024, 4, 26),
                IsUserManual = true,
                IsPublic = false,
                FileSize = 225133
            }
        ];

        repository.Setup(repo => repo.GetCurrentAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync(versions);

        var result = await new GetCurrentStaticReportsQueryHandler(repository.Object)
            .Handle(new GetCurrentStaticReportsQuery(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Title.Should().Be("D2R2 Quality Statement");
    }

    [Fact]
    public async Task GetStaticReportDataQueryHandler_ReturnsNotFound_WhenRepositoryReturnsNull()
    {
        repository.Setup(repo => repo.GetDataAsync(VersionId, It.IsAny<CancellationToken>())).ReturnsAsync((StaticReportData?)null);

        var result = await new GetStaticReportDataQueryHandler(repository.Object)
            .Handle(new GetStaticReportDataQuery(VersionId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetStaticReportDataQueryHandler_ReturnsSuccess()
    {
        var data = new StaticReportData { PdfData = [1, 2, 3], IsPublic = false, Title = "D2R2 Quality Statement" };

        repository.Setup(repo => repo.GetDataAsync(VersionId, It.IsAny<CancellationToken>())).ReturnsAsync(data);

        var result = await new GetStaticReportDataQueryHandler(repository.Object)
            .Handle(new GetStaticReportDataQuery(VersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("D2R2 Quality Statement");
        result.Value.PdfData.Should().Equal(data.PdfData);
    }

    [Fact]
    public async Task UploadStaticReportCommandHandler_ReturnsSuccess()
    {
        repository
            .Setup(repo => repo.UploadAsync("D2R2 Quality Statement", It.IsAny<byte[]>(), true, false, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new UploadStaticReportCommandHandler(repository.Object).Handle(
            new UploadStaticReportCommand("D2R2 Quality Statement", [1, 2, 3], true, false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }
}
