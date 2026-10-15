using CDC.Api.Domain.Common;
using CDC.Api.Features.BespokeReportTemplates;
using CDC.Api.Features.BespokeReportTemplates.Commands;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Api.Features.BespokeReportTemplates.Queries;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.BespokeReportTemplates;

public class BespokeReportTemplateValidatorTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();

    [Theory]
    [InlineData("My template", true)]
    [InlineData("Template 2 for Anthrax", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Has-punctuation", false)]
    [InlineData("Has <tag>", false)]
    [InlineData("Caf\u00e9", false)]
    public void SaveCommand_ValidatesTheTitleCharacters(string title, bool expected)
    {
        var result = new SaveBespokeReportTemplateCommandValidator().Validate(new SaveBespokeReportTemplateCommand(title, ["a"]));

        result.IsValid.Should().Be(expected);
    }

    [Fact]
    public void SaveCommand_AcceptsATitleOf255Characters_ButNotLonger()
    {
        var validator = new SaveBespokeReportTemplateCommandValidator();

        validator.Validate(new SaveBespokeReportTemplateCommand(new string('a', 255), [])).IsValid.Should().BeTrue();
        validator.Validate(new SaveBespokeReportTemplateCommand(new string('a', 256), [])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SaveCommand_RejectsMissingEmptyOrOverlongTickedItems_AndTooManyOfThem()
    {
        var validator = new SaveBespokeReportTemplateCommandValidator();

        validator.Validate(new SaveBespokeReportTemplateCommand("Title", null!)).IsValid.Should().BeFalse();
        validator.Validate(new SaveBespokeReportTemplateCommand("Title", [""])).IsValid.Should().BeFalse();
        validator.Validate(new SaveBespokeReportTemplateCommand("Title", [new string('a', 201)])).IsValid.Should().BeFalse();
        validator.Validate(new SaveBespokeReportTemplateCommand("Title", [new string('a', 200)])).IsValid.Should().BeTrue();
        validator.Validate(new SaveBespokeReportTemplateCommand("Title", [.. Enumerable.Range(0, 1001).Select(index => $"item{index}")])).IsValid.Should().BeFalse();
        validator.Validate(new SaveBespokeReportTemplateCommand("Title", [.. Enumerable.Range(0, 1000).Select(index => $"item{index}")])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateCommand_ValidatesTheIdAndTickedItems()
    {
        var validator = new UpdateBespokeReportTemplateCommandValidator();

        validator.Validate(new UpdateBespokeReportTemplateCommand(TemplateId, ["a"])).IsValid.Should().BeTrue();
        validator.Validate(new UpdateBespokeReportTemplateCommand(Guid.Empty, ["a"])).IsValid.Should().BeFalse();
        validator.Validate(new UpdateBespokeReportTemplateCommand(TemplateId, null!)).IsValid.Should().BeFalse();
        validator.Validate(new UpdateBespokeReportTemplateCommand(TemplateId, [""])).IsValid.Should().BeFalse();
        validator.Validate(new UpdateBespokeReportTemplateCommand(TemplateId, [new string('a', 201)])).IsValid.Should().BeFalse();
        validator.Validate(new UpdateBespokeReportTemplateCommand(TemplateId, [.. Enumerable.Range(0, 1001).Select(index => $"item{index}")])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RenameCommand_ValidatesTheIdAndTitle()
    {
        var validator = new RenameBespokeReportTemplateCommandValidator();

        validator.Validate(new RenameBespokeReportTemplateCommand(TemplateId, "Gamma")).IsValid.Should().BeTrue();
        validator.Validate(new RenameBespokeReportTemplateCommand(Guid.Empty, "Gamma")).IsValid.Should().BeFalse();
        validator.Validate(new RenameBespokeReportTemplateCommand(TemplateId, "")).IsValid.Should().BeFalse();
        validator.Validate(new RenameBespokeReportTemplateCommand(TemplateId, "Gamma!")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DeleteCommand_AndGetQuery_RequireAnId()
    {
        new DeleteBespokeReportTemplateCommandValidator().Validate(new DeleteBespokeReportTemplateCommand(Guid.Empty)).IsValid.Should().BeFalse();
        new DeleteBespokeReportTemplateCommandValidator().Validate(new DeleteBespokeReportTemplateCommand(TemplateId)).IsValid.Should().BeTrue();
        new GetBespokeReportTemplateQueryValidator().Validate(new GetBespokeReportTemplateQuery(Guid.Empty)).IsValid.Should().BeFalse();
        new GetBespokeReportTemplateQueryValidator().Validate(new GetBespokeReportTemplateQuery(TemplateId)).IsValid.Should().BeTrue();
    }
}

public class BespokeReportTemplateHandlerTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();
    private static readonly BespokeReportTemplateDto Template = new() { Id = TemplateId, Title = "Alpha", CheckedList = ["a"] };

    private readonly Mock<IBespokeReportTemplateService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetTemplatesHandler_ReturnsTheServiceResult()
    {
        var expected = Result.Success<IReadOnlyList<BespokeReportTemplateSummaryDto>>([new BespokeReportTemplateSummaryDto { Id = TemplateId, Title = "Alpha" }]);
        service.Setup(svc => svc.GetTemplatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new GetBespokeReportTemplatesQueryHandler(service.Object).Handle(new GetBespokeReportTemplatesQuery(), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetTemplateHandler_ReturnsTheServiceResult()
    {
        var expected = Result.Success(Template);
        service.Setup(svc => svc.GetTemplateAsync(TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new GetBespokeReportTemplateQueryHandler(service.Object).Handle(new GetBespokeReportTemplateQuery(TemplateId), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task SaveHandler_SavesAsANewTemplate()
    {
        var expected = Result.Success(Template);
        IReadOnlyList<string> items = ["a"];
        service.Setup(svc => svc.SaveAsNewAsync("Alpha", items, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new SaveBespokeReportTemplateCommandHandler(service.Object).Handle(new SaveBespokeReportTemplateCommand("Alpha", items), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task UpdateHandler_UpdatesTheTickedItems()
    {
        var expected = Result.Success(Template);
        IReadOnlyList<string> items = ["a"];
        service.Setup(svc => svc.UpdateCheckedListAsync(TemplateId, items, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new UpdateBespokeReportTemplateCommandHandler(service.Object).Handle(new UpdateBespokeReportTemplateCommand(TemplateId, items), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task RenameHandler_RenamesTheTemplate()
    {
        var expected = Result.Success(Template);
        service.Setup(svc => svc.RenameAsync(TemplateId, "Gamma", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new RenameBespokeReportTemplateCommandHandler(service.Object).Handle(new RenameBespokeReportTemplateCommand(TemplateId, "Gamma"), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task DeleteHandler_DeletesTheTemplate()
    {
        var expected = Result.Success(Unit.Value);
        service.Setup(svc => svc.DeleteAsync(TemplateId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new DeleteBespokeReportTemplateCommandHandler(service.Object).Handle(new DeleteBespokeReportTemplateCommand(TemplateId), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }
}

public class BespokeReportTemplatesControllerTests
{
    private static readonly Guid TemplateId = Guid.NewGuid();
    private static readonly BespokeReportTemplateDto Template = new() { Id = TemplateId, Title = "Alpha", CheckedList = ["a"] };

    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private BespokeReportTemplatesController CreateController() => new(mediator.Object)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetTemplates_ReturnsOk()
    {
        IReadOnlyList<BespokeReportTemplateSummaryDto> templates = [new() { Id = TemplateId, Title = "Alpha" }];
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetBespokeReportTemplatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(templates));

        var response = await CreateController().GetTemplates(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(templates);
    }

    [Fact]
    public async Task GetTemplates_IsForbidden_WhenTheUserMayNotViewTemplates()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetBespokeReportTemplatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<IReadOnlyList<BespokeReportTemplateSummaryDto>>("no"));

        var response = await CreateController().GetTemplates(CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetTemplate_ReturnsOk_OrNotFound()
    {
        mediator
            .SetupSequence(sender => sender.Send(It.IsAny<GetBespokeReportTemplateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Template))
            .ReturnsAsync(Result.NotFound<BespokeReportTemplateDto>("missing"));

        var found = await CreateController().GetTemplate(TemplateId, CancellationToken.None);
        var missing = await CreateController().GetTemplate(TemplateId, CancellationToken.None);

        found.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(Template);
        AssertProblem(missing.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SaveTemplateAs_ReturnsCreated_WithALocationForTheTemplate()
    {
        mediator
            .Setup(sender => sender.Send(It.Is<SaveBespokeReportTemplateCommand>(command => command.Title == "Alpha" && command.CheckedList.Count == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Template));

        var response = await CreateController().SaveTemplateAs(new SaveBespokeReportTemplateRequest { Title = "Alpha", CheckedList = ["a"] }, CancellationToken.None);

        var created = response.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(BespokeReportTemplatesController.GetTemplate));
        created.RouteValues!["id"].Should().Be(TemplateId);
        created.Value.Should().BeSameAs(Template);
    }

    [Fact]
    public async Task SaveTemplateAs_IsAConflict_WhenTheTitleIsInUse()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<SaveBespokeReportTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<BespokeReportTemplateDto>("taken"));

        var response = await CreateController().SaveTemplateAs(new SaveBespokeReportTemplateRequest { Title = "Alpha" }, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task UpdateTemplate_SendsTheRouteIdWithTheTickedItems()
    {
        mediator
            .Setup(sender => sender.Send(It.Is<UpdateBespokeReportTemplateCommand>(command => command.Id == TemplateId && command.CheckedList.Count == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Template));

        var response = await CreateController().UpdateTemplate(TemplateId, new UpdateBespokeReportTemplateRequest { CheckedList = ["a", "b"] }, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(Template);
    }

    [Fact]
    public async Task RenameTemplate_SendsTheRouteIdWithTheTitle()
    {
        mediator
            .Setup(sender => sender.Send(It.Is<RenameBespokeReportTemplateCommand>(command => command.Id == TemplateId && command.Title == "Gamma"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Template));

        var response = await CreateController().RenameTemplate(TemplateId, new RenameBespokeReportTemplateRequest { Title = "Gamma" }, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(Template);
    }

    [Fact]
    public async Task DeleteTemplate_ReturnsNoContent_OrNotFound()
    {
        mediator
            .SetupSequence(sender => sender.Send(It.IsAny<DeleteBespokeReportTemplateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value))
            .ReturnsAsync(Result.NotFound<Unit>("missing"));

        var deleted = await CreateController().DeleteTemplate(TemplateId, CancellationToken.None);
        var missing = await CreateController().DeleteTemplate(TemplateId, CancellationToken.None);

        deleted.Should().BeOfType<NoContentResult>();
        AssertProblem(missing, StatusCodes.Status404NotFound);
    }

    private static void AssertProblem(IActionResult result, int expectedStatusCode)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatusCode);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
    }

    /// <summary>Minimal factory so <c>ControllerBase.Problem()</c> works without the MVC pipeline.</summary>
    private sealed class TestProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new()
            {
                Status = statusCode ?? StatusCodes.Status500InternalServerError,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new(modelStateDictionary)
            {
                Status = statusCode ?? StatusCodes.Status400BadRequest,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
    }
}
