using CDC.Api.Domain.Common;
using CDC.Api.Features.ReferenceData;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ReferenceData;

public sealed class ReferenceDataControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ReferenceDataController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetReferenceValues_ReturnsOk()
    {
        var referenceTableId = Guid.NewGuid();
        IReadOnlyList<ReferenceValueDto> values = [new ReferenceValueDto { Id = Guid.NewGuid(), Value = "Market records" }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetReferenceValuesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(values));

        var response = await CreateController().GetReferenceValues(referenceTableId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(values);
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
