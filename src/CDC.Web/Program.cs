using System.Globalization;
using CDC.Auth.Cidm;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Events;
using CDC.Common.Correlation;
using CDC.Common.Health;
using CDC.Web.Features.Account;
using CDC.Web.Features.Health;
using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));

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

// Reference data is served from an in-process store until the reference data endpoints exist on
// CDC.Api; swap this registration for a typed HttpClient when they do.
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IReferenceDataService, InMemoryReferenceDataService>();
// In-memory pending a CDC.Api endpoint for cross-cutting issue scores; singleton so edits
// persist across requests for the lifetime of the process.
builder.Services.AddSingleton<ICrossCuttingIssueScoreService, InMemoryCrossCuttingIssueScoreService>();

// Holds uncommitted cross-cutting issue score edits until the user selects Update.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddHttpClient<IProfileContributorsApiService, ProfileContributorsApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IProfileSectionsApiService, ProfileSectionsApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IPrioritisationVariablesApiService, PrioritisationVariablesApiService>(client =>
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

// External-user auth (CIDM OIDC) and internal-user auth (Entra ID OIDC) each register their own
// Cookie + OIDC scheme pair additively, so they coexist without conflicting. AddEntraAuthentication
// must run AFTER AddCidmAuthentication - whichever call registers last wins the app-wide default
// scheme, and this app is predominantly used by internal (Entra-authenticated) staff, so Entra's
// cookie should be the fallback for every page that doesn't explicitly request CIDM.
builder.AddCidmAuthentication();
builder.AddEntraAuthentication();

// Resolves the CIDM-authenticated principal to a CDC.Api [dbo].[User] row once per sign-in - see
// CidmOpenIdConnectEvents.TokenValidated, which calls this via ICidmExternalUserResolver.
builder.Services.AddScoped<ICidmExternalUserResolver, ExternalUserResolver>();

// Resolves the Entra ID-authenticated principal to a CDC.Api [dbo].[User] row once per sign-in -
// see EntraOpenIdConnectEvents.TokenValidated, which calls this via IEntraInternalUserResolver.
builder.Services.AddScoped<IEntraInternalUserResolver, InternalUserResolver>();

// Authenticated by default - every page must opt OUT with [AllowAnonymous] rather than every new
// page having to remember to opt IN with [Authorize]. Health/Account/Landing's public pages are
// the only pages so far explicitly marked anonymous. The CIDM-specific external-user journey
// (LandingController.External) overrides this with its own explicit [Authorize(AuthenticationSchemes = ...)]
// attribute, since the fallback below resolves to Entra's cookie scheme.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

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

app.UseSession();

app.MapHealthEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}")
    .WithStaticAssets();

// Ensure Razor Pages are available if any exist in the project
app.MapRazorPages();

await app.RunAsync();
