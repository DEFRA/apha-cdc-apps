using CDC.Api.Application;
using CDC.Api.Features.Health;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Swagger;
using CDC.Api.Middleware;
using CDC.Common.Correlation;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON to stdout only - ECS/Fargate storage is ephemeral, so no file sinks. The
// awslogs driver on the container picks stdout/stderr up and ships it to CloudWatch Logs.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// Database:Host/Name/User/Password are sourced from appsettings.Development.json
// locally; in every deployed environment they come from the
// Database__Host/Database__Name/Database__User/Database__Password environment
// variables, which the ECS task definition injects from Parameter Store at
// container start - never baked into the image or read from appsettings.json.
StartupChecks.RequireDatabaseOptions(builder.Configuration);

// HealthCheck__ReadinessKey - same fail-fast reasoning: a broken secret
// wiring here would otherwise be invisible, since ReadinessKeyFilter must
// return an identical 404 for "not configured" and "wrong key" alike.
StartupChecks.RequireReadinessKey(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSwaggerDocumentation();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// First in the pipeline so it also catches failures raised by routing and model binding.
app.UseGlobalExceptionHandling();

// Correlation ID before request logging so the one-line-per-request log carries it; CDC.Web
// forwards its own ID here via CorrelationIdDelegatingHandler, so both services' logs correlate.
app.UseCorrelationId();
app.UseSerilogRequestLogging();

// Swagger is enabled in every environment: the API is reachable only from inside the VPC,
// and the deployed contract is what integrators need to read. Set Swagger:Enabled to false
// to turn it off without a code change.
if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwaggerDocumentation();
}

app.MapGet("/", () => "Hello World!");

app.MapHealthEndpoints();
app.MapControllers();

app.Run();

/// <summary>
/// Exposes the generated entry-point class to <c>WebApplicationFactory&lt;Program&gt;</c> in
/// CDC.Api.Tests.
/// </summary>
public partial class Program { }
