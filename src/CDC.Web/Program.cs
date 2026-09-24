using System.Globalization;
using CDC.Common.Correlation;
using CDC.Web.Features.Health;
using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON to stdout only - ECS/Fargate storage is ephemeral, so no file sinks. The
// awslogs driver on the container picks stdout/stderr up and ships it to CloudWatch Logs. The
// error-only file sink below is a local development aid, written under the app's own content
// root (not a shared/writable system directory) so it works the same on any machine.
var tempLogPath = Path.Combine(builder.Environment.ContentRootPath, "Logs", "cdc-web-errors.log");
Directory.CreateDirectory(Path.GetDirectoryName(tempLogPath)!);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter())
    .WriteTo.Logger(loggerConfiguration => loggerConfiguration
        .MinimumLevel.Error()
        .WriteTo.File(
            tempLogPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            formatProvider: CultureInfo.InvariantCulture,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")));

// Add services to the container.
builder.Services.AddControllersWithViews();
// Also enable Razor Pages (some projects in the solution use Razor Pages)
builder.Services.AddRazorPages();

// Typed client for calling CDC.Api. Base address comes from config (Api:BaseUrl) so local dev,
// ECS Service Connect (internal DNS alias) and any other environment just change the one value.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");

// Fail fast at startup on a malformed value (e.g. a Service Connect DNS name configured
// without an http(s):// scheme), rather than a confusing failure on the first outgoing request.
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri) ||
    (apiBaseUri.Scheme != Uri.UriSchemeHttp && apiBaseUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        $"Configuration value 'Api:BaseUrl' ('{apiBaseUrl}') must be an absolute http:// or https:// URL, e.g. 'http://cdc-api:8080'.");
}

// Forwards this request's correlation ID to CDC.Api, so a single ID traces the action across
// both services' CloudWatch log groups.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<ISpeciesApiService, SpeciesApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddStandardResilienceHandler();

builder.Services.AddHealthChecks()
    .AddCheck<ApiConnectivityHealthCheck>("api-connectivity");

// Allow views to be located under Features/{Controller}/Views and Features/Shared
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Insert(0, "/Features/{1}/Views/{0}.cshtml");
    options.ViewLocationFormats.Insert(1, "/Features/Shared/{0}.cshtml");
});

// NOTE: real authentication (Entra ID SAML for internal users, CIDM/GOV.UK One Login OIDC for
// external users) is not wired up yet. It will replace this placeholder Landing selection screen.

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Landing/Error");
}

// Correlation ID before request logging so the one-line-per-request log carries it, and so it's
// available on HttpContext.Items for CorrelationIdDelegatingHandler when Web calls the Api.
app.UseCorrelationId();
app.UseSerilogRequestLogging();

// Serve static files from wwwroot
app.UseStaticFiles();

app.UseRouting();

app.MapHealthEndpoints();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}")
    .WithStaticAssets();

// Ensure Razor Pages are available if any exist in the project
app.MapRazorPages();

await app.RunAsync();
