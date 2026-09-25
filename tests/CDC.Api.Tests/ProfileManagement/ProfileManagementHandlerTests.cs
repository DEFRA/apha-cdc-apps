using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileManagement.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

public class ProfileManagementHandlerTests
{
    private readonly Mock<IProfileManagementService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task CreateProfileCommandHandler_ReturnsSuccess()
    {
        var command = new CreateProfileCommand { Id = ProfileManagementTestData.ProfileId, Title = "Anthrax" };
        var resultDto = new CreateProfileResultDto { NewProfileId = command.Id, NewLastUpdated = ProfileManagementTestData.RowVersion };

        service
            .Setup(svc => svc.CreateProfileAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await new CreateProfileCommandHandler(service.Object).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task UpdateProfileAttributesCommandHandler_ReturnsSuccess()
    {
        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            LastUpdated = ProfileManagementTestData.RowVersion
        };
        var resultDto = new UpdateProfileAttributesResultDto { NewLastUpdated = ProfileManagementTestData.NewRowVersion };

        service
            .Setup(svc => svc.UpdateProfileAttributesAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await new UpdateProfileAttributesCommandHandler(service.Object, NullLogger<UpdateProfileAttributesCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task UpdateProfileAttributesCommandHandler_ReturnsConflict_OnConcurrencyException()
    {
        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            LastUpdated = ProfileManagementTestData.RowVersion
        };

        service
            .Setup(svc => svc.UpdateProfileAttributesAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await new UpdateProfileAttributesCommandHandler(service.Object, NullLogger<UpdateProfileAttributesCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task DeleteProfileVersionCommandHandler_ReturnsNotFound_WhenServiceReturnsNull()
    {
        service
            .Setup(svc => svc.DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeleteProfileVersionResultDto?)null);

        var result = await new DeleteProfileVersionCommandHandler(service.Object)
            .Handle(new DeleteProfileVersionCommand(ProfileManagementTestData.ProfileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task DeleteProfileVersionCommandHandler_ReturnsSuccess()
    {
        var resultDto = new DeleteProfileVersionResultDto { IsProfileDeleted = false, NextLatestProfileVersionId = Guid.NewGuid() };

        service
            .Setup(svc => svc.DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await new DeleteProfileVersionCommandHandler(service.Object)
            .Handle(new DeleteProfileVersionCommand(ProfileManagementTestData.ProfileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task CreateNewProfileVersionCommandHandler_ReturnsConflict_OnConcurrencyException()
    {
        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);

        service
            .Setup(svc => svc.CreateNewProfileVersionAsync(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("not the latest version"));

        var result = await new CreateNewProfileVersionCommandHandler(service.Object, NullLogger<CreateNewProfileVersionCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task CreateNewProfileVersionCommandHandler_ReturnsSuccess()
    {
        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);
        var resultDto = new NewProfileVersionResultDto { NewProfileVersionId = ProfileManagementTestData.NewProfileVersionId };

        service
            .Setup(svc => svc.CreateNewProfileVersionAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var result = await new CreateNewProfileVersionCommandHandler(service.Object, NullLogger<CreateNewProfileVersionCommandHandler>.Instance)
            .Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task SetProfileVersionPublicAccessCommandHandler_ReturnsSuccess()
    {
        service
            .Setup(svc => svc.SetProfileVersionPublicAccessAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new SetProfileVersionPublicAccessCommandHandler(service.Object)
            .Handle(new SetProfileVersionPublicAccessCommand(ProfileManagementTestData.ProfileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task UpdateProfileStatusCommandHandler_ReturnsNotFound_WhenStatusUnknown()
    {
        service
            .Setup(svc => svc.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusTypeDto>)[]);

        var result = await new UpdateProfileStatusCommandHandler(service.Object)
            .Handle(new UpdateProfileStatusCommand(ProfileManagementTestData.ProfileId, ProfileManagementTestData.ProfileStatusId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateProfileStatusCommandHandler_ReturnsSuccess_WhenStatusKnown()
    {
        IReadOnlyList<ProfileStatusTypeDto> statusTypes = [new ProfileStatusTypeDto { Id = ProfileManagementTestData.ProfileStatusId }];

        service
            .Setup(svc => svc.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusTypes);
        service
            .Setup(svc => svc.UpdateProfileStatusAsync(
                ProfileManagementTestData.ProfileId,
                ProfileManagementTestData.ProfileStatusId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new UpdateProfileStatusCommandHandler(service.Object)
            .Handle(new UpdateProfileStatusCommand(ProfileManagementTestData.ProfileId, ProfileManagementTestData.ProfileStatusId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfileAttributesQueryHandler_ReturnsNotFound_WhenServiceReturnsNull()
    {
        service
            .Setup(svc => svc.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileAttributesDto?)null);

        var result = await new GetProfileAttributesQueryHandler(service.Object)
            .Handle(new GetProfileAttributesQuery(ProfileManagementTestData.ProfileId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetProfileAttributesQueryHandler_ReturnsSuccess()
    {
        var dto = ProfileManagementTestData.ProfileAttributesDto();

        service
            .Setup(svc => svc.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileAttributesQueryHandler(service.Object)
            .Handle(new GetProfileAttributesQuery(ProfileManagementTestData.ProfileId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileStatusTypesQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileStatusTypeDto> statusTypes = [new ProfileStatusTypeDto { Id = ProfileManagementTestData.ProfileStatusId }];

        service
            .Setup(svc => svc.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusTypes);

        var result = await new GetProfileStatusTypesQueryHandler(service.Object).Handle(new GetProfileStatusTypesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(statusTypes);
    }
}
