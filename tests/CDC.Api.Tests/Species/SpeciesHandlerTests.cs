using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.Species.Commands;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using CDC.Api.Features.Species.Queries;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.Species;

public class SpeciesHandlerTests
{
    private readonly Mock<ISpeciesService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetAllSpeciesQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<SpeciesDto> species = [new SpeciesDto { Id = SpeciesTestData.SpeciesId }];
        service.Setup(svc => svc.GetAllSpeciesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(species);

        var result = await new GetAllSpeciesQueryHandler(service.Object)
            .Handle(new GetAllSpeciesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(species);
    }

    [Fact]
    public async Task GetSpeciesMetadataQueryHandler_ReturnsSuccess()
    {
        var metadata = new SpeciesMetadataDto();
        service.Setup(svc => svc.GetSpeciesMetadataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(metadata);

        var result = await new GetSpeciesMetadataQueryHandler(service.Object)
            .Handle(new GetSpeciesMetadataQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(metadata);
    }

    [Fact]
    public async Task GetAllSelectedSpeciesQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<SelectedSpeciesDto> species = [new SelectedSpeciesDto()];
        service
            .Setup(svc => svc.GetAllSelectedSpeciesAsync("Anthrax", It.IsAny<CancellationToken>()))
            .ReturnsAsync(species);

        var result = await new GetAllSelectedSpeciesQueryHandler(service.Object)
            .Handle(new GetAllSelectedSpeciesQuery("Anthrax"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(species);
    }

    [Fact]
    public async Task GetSpeciesAnswerDataQueryHandler_ReturnsNotFound_WhenTheServiceReturnsNull()
    {
        service
            .Setup(svc => svc.GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpeciesAnswerDataDto?)null);

        var result = await new GetSpeciesAnswerDataQueryHandler(service.Object)
            .Handle(new GetSpeciesAnswerDataQuery(SpeciesTestData.SpeciesId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Error.Should().Contain(SpeciesTestData.SpeciesId.ToString());
    }

    [Fact]
    public async Task GetSpeciesAnswerDataQueryHandler_ReturnsSuccess()
    {
        var answerData = new SpeciesAnswerDataDto { SpeciesId = SpeciesTestData.SpeciesId };
        service
            .Setup(svc => svc.GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answerData);

        var result = await new GetSpeciesAnswerDataQueryHandler(service.Object)
            .Handle(new GetSpeciesAnswerDataQuery(SpeciesTestData.SpeciesId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(answerData);
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataCommandHandler_ReturnsSuccess()
    {
        var command = SpeciesTestData.UpdateCommand();
        var updateResult = new UpdateSpeciesAnswerDataResultDto { SpeciesId = SpeciesTestData.SpeciesId };

        service
            .Setup(svc => svc.UpdateSpeciesAnswerDataAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult);

        var result = await CreateCommandHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(updateResult);
    }

    [Fact]
    public async Task UpdateSpeciesAnswerDataCommandHandler_ReturnsConflict_WhenTheRepositoryDetectsAConcurrentEdit()
    {
        var command = SpeciesTestData.UpdateCommand();

        service
            .Setup(svc => svc.UpdateSpeciesAnswerDataAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("Edited by another user."));

        var result = await CreateCommandHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("Edited by another user.");
    }

    [Fact]
    public void Result_Value_ThrowsForAFailedResult()
    {
        var result = Result.NotFound<SpeciesDto>("missing");

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task GetSpeciesDetailQueryHandler_ReturnsSuccess()
    {
        var detail = new SpeciesDetailDto { Id = SpeciesTestData.SpeciesId, Name = "Dairy cattle" };
        service
            .Setup(svc => svc.GetSpeciesDetailAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var result = await new GetSpeciesDetailQueryHandler(service.Object)
            .Handle(new GetSpeciesDetailQuery(SpeciesTestData.SpeciesId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(detail);
    }

    [Fact]
    public async Task GetSpeciesDetailQueryHandler_ReturnsNotFound_WhenTheServiceReturnsNull()
    {
        service
            .Setup(svc => svc.GetSpeciesDetailAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpeciesDetailDto?)null);

        var result = await new GetSpeciesDetailQueryHandler(service.Object)
            .Handle(new GetSpeciesDetailQuery(SpeciesTestData.SpeciesId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetSpeciesValidParentsQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<SpeciesValidParentDto> validParents = [new SpeciesValidParentDto { Id = SpeciesTestData.SectionId, Name = "Cattle" }];
        service
            .Setup(svc => svc.GetSpeciesValidParentsAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validParents);

        var result = await new GetSpeciesValidParentsQueryHandler(service.Object)
            .Handle(new GetSpeciesValidParentsQuery(SpeciesTestData.SpeciesId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(validParents);
    }

    [Fact]
    public async Task GetSpeciesAuditTrailQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<SpeciesAuditTrailEntryDto> auditTrail = [new SpeciesAuditTrailEntryDto { Id = SpeciesTestData.FieldId }];
        service
            .Setup(svc => svc.GetSpeciesAuditTrailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditTrail);

        var result = await new GetSpeciesAuditTrailQueryHandler(service.Object)
            .Handle(new GetSpeciesAuditTrailQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(auditTrail);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentCommandHandler_ReturnsSuccess()
    {
        var command = SpeciesTestData.UpdateNameParentCommand();
        var updateResult = new UpdateSpeciesNameParentResultDto { SpeciesId = SpeciesTestData.SpeciesId };

        service
            .Setup(svc => svc.UpdateSpeciesNameParentAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updateResult);

        var result = await CreateNameParentCommandHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(updateResult);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentCommandHandler_ReturnsConflict_WhenTheRepositoryDetectsAConcurrentEdit()
    {
        var command = SpeciesTestData.UpdateNameParentCommand();

        service
            .Setup(svc => svc.UpdateSpeciesNameParentAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("Edited by another user."));

        var result = await CreateNameParentCommandHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("Edited by another user.");
    }

    private UpdateSpeciesNameParentCommandHandler CreateNameParentCommandHandler() =>
        new(service.Object, NullLogger<UpdateSpeciesNameParentCommandHandler>.Instance);

    private UpdateSpeciesAnswerDataCommandHandler CreateCommandHandler() =>
        new(service.Object, NullLogger<UpdateSpeciesAnswerDataCommandHandler>.Instance);
}
