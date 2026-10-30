using CDC.Api.Features.SpellCheck;
using CDC.Api.Features.SpellCheck.Dtos;
using CDC.Api.Features.SpellCheck.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CDC.Api.Tests.SpellCheck;

public sealed class SpellCheckControllerTests
{
    private readonly Mock<ISpellCheckService> service = new(MockBehavior.Strict);

    private SpellCheckController CreateController() => new(service.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetCustomWords_ReturnsOkWithTheWords()
    {
        IReadOnlyList<string> words = ["Defra", "APHA"];
        service.Setup(svc => svc.GetCustomWordsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(words);

        var response = await CreateController().GetCustomWords(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(words);
    }

    [Fact]
    public async Task AddCustomWord_ReturnsNoContent_WhenTheWordIsInTheDictionary()
    {
        service
            .Setup(svc => svc.AddCustomWordAsync("Defra", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddCustomWordResult(AddCustomWordOutcome.Success, null));

        var response = await CreateController().AddCustomWord(new AddCustomWordRequestDto { Word = "Defra" }, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Theory]
    [InlineData(AddCustomWordOutcome.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(AddCustomWordOutcome.ValidationFailed, StatusCodes.Status400BadRequest)]
    public async Task AddCustomWord_ReturnsAProblem_WhenTheWordIsRefused(AddCustomWordOutcome outcome, int expectedStatusCode)
    {
        service
            .Setup(svc => svc.AddCustomWordAsync("Defra", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddCustomWordResult(outcome, "Refused."));

        var response = await CreateController().AddCustomWord(new AddCustomWordRequestDto { Word = "Defra" }, CancellationToken.None);

        var problem = response.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatusCode);
        problem.Value.Should().BeOfType<ProblemDetails>().Which.Detail.Should().Be("Refused.");
    }
}
