using System.Reflection;
using CDC.Api.Application.Extensions;
using CDC.Api.Domain.Common;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Tests.Application;

public class ResultExtensionsTests
{
    [Fact]
    public void ToActionResult_WithSuccess_ReturnsOkWithValue()
    {
        var result = Result.Success("Hello");
        var controller = new TestController();

        var actionResult = result.ToActionResult(controller);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().Be("Hello");
    }

    [Fact]
    public void ToActionResult_WithNotFound_ReturnsProblemWithStatus404()
    {
        var result = Result.NotFound<string>("Item not found.");
        var controller = new TestController();

        var actionResult = result.ToActionResult(controller);

        var problem = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToActionResult_WithConflict_ReturnsProblemWithStatus409()
    {
        var result = Result.Conflict<string>("Edited by another user.");
        var controller = new TestController();

        var actionResult = result.ToActionResult(controller);

        var problem = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ToActionResult_WithUnexpectedStatus_ReturnsProblemWithStatus500()
    {
        // Create a Result with an out-of-range status to hit the default branch.
        // Since Result<T> ctor is internal but the test assembly has InternalsVisibleTo,
        // we use reflection to construct with an invalid status.
        var ctor = typeof(Result<>).MakeGenericType(typeof(string)).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [typeof(ResultStatus), typeof(string), typeof(string)],
            null)!;
        var unexpectedResult = (Result<string>)ctor.Invoke([(ResultStatus)99, null, "Something unexpected"])!;

        var controller = new TestController();

        var actionResult = unexpectedResult.ToActionResult(controller);

        var problem = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void ToActionResult_WithDependencyFailed_ReturnsProblemWithStatus502()
    {
        var actionResult = Result.DependencyFailed<string>("Email could not be sent.").ToActionResult(new TestController());

        var problem = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Theory]
    [InlineData(ResultStatus.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ResultStatus.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ResultStatus.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ResultStatus.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(ResultStatus.DependencyFailed, StatusCodes.Status502BadGateway)]
    public void ToNoContentActionResult_MapsEachFailureToItsStatusCode(ResultStatus status, int expectedStatusCode)
    {
        Result<Unit> result = status switch
        {
            ResultStatus.NotFound => Result.NotFound<Unit>("failed"),
            ResultStatus.Conflict => Result.Conflict<Unit>("failed"),
            ResultStatus.Forbidden => Result.Forbidden<Unit>("failed"),
            ResultStatus.ValidationFailed => Result.ValidationFailed<Unit>("failed"),
            _ => Result.DependencyFailed<Unit>("failed")
        };

        var actionResult = result.ToNoContentActionResult(new TestController());

        actionResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(expectedStatusCode);
    }

    [Fact]
    public void ToNoContentActionResult_WithSuccess_ReturnsNoContent() =>
        Result.Success(Unit.Value).ToNoContentActionResult(new TestController()).Should().BeOfType<NoContentResult>();

    [Fact]
    public void ToActionResult_WithNullResult_ThrowsArgumentNullException()
    {
        Result<string>? result = null;
        var controller = new TestController();

        var act = () => result!.ToActionResult(controller);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToActionResult_WithNullController_ThrowsArgumentNullException()
    {
        var result = Result.Success("Hello");

        var act = () => result.ToActionResult(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class TestController : ControllerBase
    {
        public TestController()
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }
    }
}
