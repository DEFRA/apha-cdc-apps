using System.Text.Json;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Middleware;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Api.Tests.Middleware;

public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PassesThrough_WhenNothingThrows()
    {
        var context = CreateContext();

        await CreateMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound, "Resource not found")]
    [InlineData(typeof(ConcurrencyException), StatusCodes.Status409Conflict, "Conflicting change")]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status403Forbidden, "Forbidden")]
    public async Task InvokeAsync_MapsKnownExceptionsToProblemDetails(Type exceptionType, int expectedStatus, string expectedTitle)
    {
        var context = CreateContext();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        await CreateMiddleware(_ => throw exception).InvokeAsync(context);

        var problem = await ReadProblemAsync(context);
        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.ContentType.Should().Be("application/problem+json");
        problem.GetProperty("title").GetString().Should().Be(expectedTitle);
        problem.GetProperty("instance").GetString().Should().Be("/api/species");
        problem.TryGetProperty("traceId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ReturnsValidationProblem_ForAValidationException()
    {
        var context = CreateContext();
        var failures = new[] { new ValidationFailure("SpeciesId", "A species id is required.") };

        await CreateMiddleware(_ => throw new ValidationException(failures)).InvokeAsync(context);

        var problem = await ReadProblemAsync(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.GetProperty("errors").GetProperty("SpeciesId")[0].GetString()
            .Should().Be("A species id is required.");
    }

    [Fact]
    public async Task InvokeAsync_HidesDatabaseDetail_OutsideDevelopment()
    {
        var context = CreateContext();

        await CreateMiddleware(_ => throw new FakeDbException("Login failed for user 'sa'"), "Production")
            .InvokeAsync(context);

        var problem = await ReadProblemAsync(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problem.GetProperty("title").GetString().Should().Be("Database error");
        problem.GetProperty("detail").GetString().Should().NotContain("sa");
    }

    [Fact]
    public async Task InvokeAsync_IncludesDetail_InDevelopment()
    {
        var context = CreateContext();

        await CreateMiddleware(_ => throw new InvalidOperationException("Boom"), "Development").InvokeAsync(context);

        var problem = await ReadProblemAsync(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problem.GetProperty("detail").GetString().Should().Be("Boom");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsClientClosedRequest_WhenTheRequestIsCancelled()
    {
        var context = CreateContext();

        await CreateMiddleware(_ => throw new OperationCanceledException()).InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotWrite_WhenTheResponseHasAlreadyStarted()
    {
        var context = CreateContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var act = async () => await CreateMiddleware(_ => throw new InvalidOperationException("Boom")).InvokeAsync(context);

        await act.Should().NotThrowAsync();
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/species";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static GlobalExceptionMiddleware CreateMiddleware(RequestDelegate next, string environmentName = "Production") =>
        new(next, NullLogger<GlobalExceptionMiddleware>.Instance, new StubEnvironment(environmentName));

    private static async Task<JsonElement> ReadProblemAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        return document.RootElement.Clone();
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "CDC.Api.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public string EnvironmentName { get; set; } = environmentName;
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public string? ReasonPhrase { get; set; }

        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public void OnStarting(Func<object, Task> callback, object state)
        {
        }
    }
}
