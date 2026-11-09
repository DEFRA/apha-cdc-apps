using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ReferenceData;
using CDC.Api.Features.ReferenceData.Commands;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Features.ReferenceData.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ReferenceData;

public sealed class ReferenceDataMaintenanceTests
{
    private static readonly Guid TableId = Guid.Parse("D9889426-96A2-4C69-A3DB-C26F256C5FC1");
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly byte[] RowVersion = [0, 0, 0, 0, 0, 0, 7, 209];

    private static readonly MaintainableReferenceValueDto InUse = new()
    {
        Id = Guid.Parse("533262C1-454C-45E3-876B-2B47841AB5C2"), LookupValue = "Import controls", IsInUse = true, IsActive = true
    };

    private static readonly MaintainableReferenceValueDto NotInUse = new()
    {
        Id = Guid.Parse("39D5AE2D-29C9-4958-8A83-BA9646B6B136"), LookupValue = "Zoning", IsInUse = false, IsActive = true
    };

    private static readonly MaintainableReferenceValueDto Inactive = new()
    {
        Id = Guid.Parse("F3E04DBB-AEFE-4157-8F69-8BEFD5A8BE97"), LookupValue = "Old", IsInUse = false, IsActive = false
    };

    private readonly Mock<IReferenceDataMaintenanceRepository> repository = new();

    public ReferenceDataMaintenanceTests() =>
        repository.Setup(r => r.GetMaintainableValuesAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([InUse, NotInUse, Inactive]);

    private ChangeReferenceValueCommandHandler Handler => new(repository.Object);

    private static ChangeReferenceValueCommand Command(ReferenceValueChangeAction action, Guid? valueId = null, string? newValue = null) => new()
    {
        ReferenceTableId = TableId,
        Action = action,
        ValueId = valueId,
        NewLookupValue = newValue,
        Reason = "  Because  ",
        LastUpdated = RowVersion,
        UserId = UserId
    };

    [Fact]
    public async Task Add_InsertsTheTrimmedValueWithANewId()
    {
        var result = await Handler.Handle(Command(ReferenceValueChangeAction.Add, newValue: " Vaccination "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(r => r.InsertValueAsync(TableId, result.Value.ValueId, "Vaccination", UserId, "Because", It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(ReferenceValueChangeAction.Add, null)]
    [InlineData(ReferenceValueChangeAction.Edit, "533262C1-454C-45E3-876B-2B47841AB5C2")]
    public async Task AddAndEdit_RejectADuplicate_CaseInsensitively(ReferenceValueChangeAction action, string? valueId)
    {
        var result = await Handler.Handle(
            Command(action, valueId is null ? null : Guid.Parse(valueId), "ZONING"), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be(DuplicateReferenceValueException.DefaultMessage);
    }

    [Fact]
    public async Task Edit_UpdatesTheValue()
    {
        var result = await Handler.Handle(Command(ReferenceValueChangeAction.Edit, InUse.Id, "Import restrictions"), CancellationToken.None);

        result.Value.ValueId.Should().Be(InUse.Id);
        repository.Verify(r => r.UpdateValueAsync(InUse.Id, "Import restrictions", UserId, "Because", RowVersion, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Inactivate_EndDatesTheValue()
    {
        var result = await Handler.Handle(Command(ReferenceValueChangeAction.Inactivate, InUse.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(r => r.InactivateValueAsync(InUse.Id, UserId, "Because", RowVersion, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Delete_PassesTheOldValue_ForAValueNotInUse()
    {
        var result = await Handler.Handle(Command(ReferenceValueChangeAction.Delete, NotInUse.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(r => r.DeleteValueAsync(NotInUse.Id, "Zoning", UserId, "Because", RowVersion, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(ReferenceValueChangeAction.Delete, "533262C1-454C-45E3-876B-2B47841AB5C2")]
    [InlineData(ReferenceValueChangeAction.Edit, "F3E04DBB-AEFE-4157-8F69-8BEFD5A8BE97")]
    [InlineData(ReferenceValueChangeAction.Inactivate, "00000000-0000-0000-0000-000000000099")]
    public async Task RejectsAChangeLegacyDidNotAllow(ReferenceValueChangeAction action, string valueId)
    {
        var result = await Handler.Handle(Command(action, Guid.Parse(valueId), "New"), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
        repository.Verify(r => r.DeleteValueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdateValueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenTheTableIsNotMaintainable()
    {
        repository.Setup(r => r.GetMaintainableValuesAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<MaintainableReferenceValueDto>?)null);

        var result = await Handler.Handle(Command(ReferenceValueChangeAction.Add, newValue: "New"), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task MapsTheRepositorysExceptions()
    {
        repository.Setup(r => r.InactivateValueAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));
        repository.Setup(r => r.InsertValueAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateReferenceValueException());

        (await Handler.Handle(Command(ReferenceValueChangeAction.Inactivate, InUse.Id), CancellationToken.None))
            .Status.Should().Be(ResultStatus.Conflict);
        (await Handler.Handle(Command(ReferenceValueChangeAction.Add, newValue: "Raced"), CancellationToken.None))
            .Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Fact]
    public void Validator_RequiresTheLegacyFields()
    {
        var validator = new ChangeReferenceValueCommandValidator();

        validator.Validate(Command(ReferenceValueChangeAction.Edit, InUse.Id, "New")).IsValid.Should().BeTrue();
        validator.Validate(Command(ReferenceValueChangeAction.Add, newValue: "New") with { LastUpdated = null }).IsValid.Should().BeTrue();

        var invalid = validator.Validate(new ChangeReferenceValueCommand
        {
            ReferenceTableId = TableId,
            Action = ReferenceValueChangeAction.Edit,
            UserId = UserId,
            LastUpdated = [1, 2]
        });

        invalid.Errors.Select(error => error.ErrorMessage).Should().Contain(
        [
            "You need to provide a reason for this change",
            "You need to provide a new value",
            "A reference value id is required.",
            "The row version must be 8 bytes."
        ]);
    }

    [Fact]
    public async Task ValueQuery_AddsTheRowVersion_OrReturnsNotFound()
    {
        repository.Setup(r => r.GetValueRowVersionAsync(InUse.Id, It.IsAny<CancellationToken>())).ReturnsAsync(RowVersion);
        var handler = new GetMaintainableReferenceValueQueryHandler(repository.Object);

        var found = await handler.Handle(new GetMaintainableReferenceValueQuery(TableId, InUse.Id), CancellationToken.None);
        var missing = await handler.Handle(new GetMaintainableReferenceValueQuery(TableId, Guid.NewGuid()), CancellationToken.None);

        found.Value.LastUpdated.Should().Equal(RowVersion);
        missing.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ValuesQuery_ReturnsNotFound_ForATableThatIsNotMaintainable()
    {
        var handler = new GetMaintainableReferenceValuesQueryHandler(repository.Object);

        (await handler.Handle(new GetMaintainableReferenceValuesQuery(TableId), CancellationToken.None)).Value.Should().HaveCount(3);
        (await handler.Handle(new GetMaintainableReferenceValuesQuery(Guid.NewGuid()), CancellationToken.None)).Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task AuditTrailQuery_NamesTheTable_OrReturnsNotFound()
    {
        repository.Setup(r => r.GetMaintainableTablesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MaintainableReferenceTableDto { Id = TableId, Name = "Control mechanism" }]);
        repository.Setup(r => r.GetAuditTrailAsync(TableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReferenceTableAuditEntryDto { Id = Guid.NewGuid(), ReferenceTableId = TableId }]);
        var handler = new GetReferenceTableAuditTrailQueryHandler(repository.Object);

        var found = await handler.Handle(new GetReferenceTableAuditTrailQuery(TableId), CancellationToken.None);
        var missing = await handler.Handle(new GetReferenceTableAuditTrailQuery(Guid.NewGuid()), CancellationToken.None);

        found.Value.Should().ContainSingle().Which.TableName.Should().Be("Control mechanism");
        missing.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task TablesQuery_ReturnsTheTables()
    {
        repository.Setup(r => r.GetMaintainableTablesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MaintainableReferenceTableDto { Id = TableId, Name = "Control mechanism" }]);

        var result = await new GetMaintainableReferenceTablesQueryHandler(repository.Object)
            .Handle(new GetMaintainableReferenceTablesQuery(), CancellationToken.None);

        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task Controller_RecordsTheSignedInUser_NotOneFromTheBody()
    {
        var mediator = new Mock<ISender>();
        ChangeReferenceValueCommand? sent = null;
        mediator.Setup(m => m.Send(It.IsAny<ChangeReferenceValueCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ChangeReferenceValueResultDto>>, CancellationToken>((command, _) => sent = (ChangeReferenceValueCommand)command)
            .ReturnsAsync(Result.Success(new ChangeReferenceValueResultDto { ValueId = InUse.Id }));
        var userContext = new Mock<IUserContext>();
        userContext.Setup(context => context.UserId).Returns(UserId);

        var response = await CreateController(mediator.Object, userContext.Object).ChangeValue(
            TableId,
            new ChangeReferenceValueRequestDto { Action = ReferenceValueChangeAction.Edit, ValueId = InUse.Id, NewLookupValue = "New", Reason = "Why", LastUpdated = RowVersion },
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        sent!.UserId.Should().Be(UserId);
        sent.ReferenceTableId.Should().Be(TableId);
    }

    [Fact]
    public async Task Controller_ReadEndpoints_ReturnOk()
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetMaintainableReferenceTablesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<MaintainableReferenceTableDto>>([]));
        mediator.Setup(m => m.Send(It.IsAny<GetMaintainableReferenceValuesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<MaintainableReferenceValueDto>>([]));
        mediator.Setup(m => m.Send(It.IsAny<GetMaintainableReferenceValueQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<MaintainableReferenceValueDto>("missing"));
        mediator.Setup(m => m.Send(It.IsAny<GetReferenceTableAuditTrailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<ReferenceTableAuditEntryDto>>([]));
        var controller = CreateController(mediator.Object, Mock.Of<IUserContext>());

        (await controller.GetTables(CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetValues(TableId, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetAuditTrail(TableId, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.GetValue(TableId, InUse.Id, CancellationToken.None)).Result
            .Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static MaintainableReferenceDataController CreateController(ISender mediator, IUserContext userContext) => new(mediator, userContext)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private sealed class TestProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new() { Status = statusCode, Title = title, Detail = detail };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new(modelStateDictionary) { Status = statusCode };
    }
}
