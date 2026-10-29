using System.Globalization;
using CDC.Auth.Cidm;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Events;
using CDC.Common.Correlation;
using CDC.Common.Health;
using CDC.Web.Authorization;
using CDC.Web.Features.Account;
using CDC.Web.Features.DiseaseRanking;
using CDC.Web.Features.Health;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
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

// Forwards the signed-in user's [User].Id to CDC.Api, so changes are recorded against and
// authorised for that user rather than a placeholder. Attach it to every typed client below.
builder.Services.AddTransient<ActingUserDelegatingHandler>();

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IStaticReportDocumentApiService, StaticReportDocumentApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<ISpeciesApiService, SpeciesApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

// Creates and updates profiles from the Profile properties page. Writes are not retried: an
// attempt can succeed and still time out, and replaying a create would fail on the profile id the
// first attempt already inserted.
builder.Services.AddHttpClient<IProfilePropertiesApiService, ProfilePropertiesApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

// Reads and saves question titles and guidance on the Edit user guidance page, and reads every question's guidance for the
// guidance reports, which CDC.Api can take longer than the default 10-second attempt timeout to gather. A save is not retried: the
// row version it carries would make a replay of one that already succeeded fail as a conflict.
builder.Services.AddHttpClient<IProfileQuestionsApiService, ProfileQuestionsApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.DisableForUnsafeHttpMethods();
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    });

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
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IGlobalUsersApiService, GlobalUsersApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IExternalUsersApiService, ExternalUsersApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IProfileSectionsApiService, ProfileSectionsApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

// The contributions report reads every draft version's sections, so CDC.Api can take longer than the default 10-second attempt timeout.
builder.Services.AddHttpClient<IProfileReportsApiService, ProfileReportsApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    });

// One Chromium browser is shared for the app's lifetime, so this must be a singleton.
builder.Services.AddSingleton<IPlaywrightPdfService, PlaywrightPdfService>();
builder.Services.AddScoped<IRazorViewRenderer, RazorViewRenderer>();
builder.Services.AddScoped<IProfileReportGenerator, ProfileReportGenerator>();
builder.Services.AddScoped<ProfileReportReferenceLoader>();
builder.Services.AddScoped<IProfileReportPdfBuilder, SummaryProfileReportPdfBuilder>();
builder.Services.AddScoped<IProfileReportPdfBuilder, QABriefReportPdfBuilder>();
builder.Services.AddScoped<IProfileReportPdfBuilder, SummaryPrioritisationReportPdfBuilder>();
builder.Services.AddScoped<FullProfileReportPdfBuilder>();
builder.Services.AddScoped<IProfileReportPdfBuilder>(services => services.GetRequiredService<FullProfileReportPdfBuilder>());
builder.Services.AddScoped<IBespokeReportPdfBuilder, BespokeReportPdfBuilder>();

builder.Services.AddHttpClient<IBespokeReportTemplatesApiService, BespokeReportTemplatesApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<IPrioritisationVariablesApiService, PrioritisationVariablesApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddScoped<IProfileVersionComparisonService, ProfileVersionComparisonService>();

// Disease ranking reports; forwards the correlation ID so the report build is traceable across both services.
builder.Services.AddHttpClient<IDiseaseRankingApiService, DiseaseRankingApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

// Disease ranking PDFs: report data from CDC.Api -> Razor view -> HTML -> headless Chromium -> PDF.
// The renderer holds one shared browser, so it is a singleton.
builder.Services.Configure<PdfRendererOptions>(builder.Configuration.GetSection(PdfRendererOptions.SectionName));
builder.Services.AddSingleton<CDC.Web.Features.DiseaseRanking.IPdfRenderer, CDC.Web.Features.DiseaseRanking.PlaywrightPdfRenderer>();
builder.Services.AddSingleton<CDC.Web.Infrastructure.IPdfRenderer, CDC.Web.Infrastructure.PlaywrightPdfRenderer>();
builder.Services.AddScoped<IRazorViewToStringRenderer, RazorViewToStringRenderer>();
builder.Services.AddScoped<IDiseaseRankingPdfService, DiseaseRankingPdfService>();

builder.Services.AddHttpClient<IDiseaseRankingFiltersApiService, DiseaseRankingFiltersApiService>(client =>
{
    client.BaseAddress = apiBaseUri;
})
    .AddHttpMessageHandler<ActingUserDelegatingHandler>()
    .AddStandardResilienceHandler();

builder.Services.AddHealthChecks()
    .AddCheck<ApiConnectivityHealthCheck>("api-connectivity");

// Centralized policy-based authorization (replaces legacy CSLA Profile.CanXxx() checks). See
// src/CDC.Web/Authorization/AuthorizationDependencyInjection.cs.
builder.Services.AddCdcWebAuthorization();

// Allow views to be located under Features/{Controller}/Views and Features/Shared
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Insert(0, "/Features/{1}/Views/{0}.cshtml");
    options.ViewLocationFormats.Insert(1, "/Features/Shared/{0}.cshtml");
});

// External-user auth (CIDM OIDC) and internal-user auth (Entra ID OIDC) each register their own
// Cookie + OIDC scheme pair additively, so they coexist without conflicting. Neither is the app's default scheme:
// AddCdcWebAuthorization makes InternalOrExternalAuthenticationScheme the default, so every request is authenticated
// with whichever session cookie it carries and a signed-in user is never sent to the other provider's sign-in.
builder.AddCidmAuthentication();
builder.AddEntraAuthentication();

// How long a signed-in session may sit idle - the lifetime of both sign-in cookies, which any request renews - and how long before
// it ends the user is asked whether to extend it. See SessionController and wwwroot/js/session-timeout.js for the dialogs.
builder.Services.AddSessionTimeout(builder.Configuration);

// Resolves the CIDM-authenticated principal to a CDC.Api [dbo].[User] row once per sign-in - see
// CidmOpenIdConnectEvents.TokenValidated, which calls this via ICidmExternalUserResolver.
builder.Services.AddScoped<ICidmExternalUserResolver, ExternalUserResolver>();

// Resolves the Entra ID-authenticated principal to a CDC.Api [dbo].[User] row once per sign-in -
// see EntraOpenIdConnectEvents.TokenValidated, which calls this via IEntraInternalUserResolver.
builder.Services.AddScoped<IEntraInternalUserResolver, InternalUserResolver>();

// Resolves the signed-in user's display name for the page header, independent of provider - see
// src/CDC.Web/Features/Account/UserDisplayNameService.cs. Stateless, so a singleton is safe.
builder.Services.AddSingleton<IUserDisplayNameService, UserDisplayNameService>();

// Authenticated by default - every page must opt OUT with [AllowAnonymous] rather than every new
// page having to remember to opt IN with [Authorize]. Health/Account/Landing's public pages are
// the only pages so far explicitly marked anonymous. The external landing page (LandingController.External) names the
// CIDM cookie in its own [Authorize(AuthenticationSchemes = ...)], so a user signed in only with Entra ID is sent to CIDM.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

// Must run before anything reads Request.Scheme/Host (including UseExceptionHandler and the OIDC
// handlers) - the ALB terminates TLS and talks plain HTTP to this ECS task, so without this,
// Request.Scheme is always "http" and the OIDC redirect_uri sent to Entra/CIDM is built wrong.
// KnownNetworks/KnownProxies are cleared because the ALB's ENI is never in the default loopback-
// only trusted list, and it's the only thing that can reach this task (private subnet, no public
// ingress), so trusting any upstream proxy here is safe.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

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

// Streams static report PDFs through this app: CDC.Api is private, so browsers cannot link to it directly. The user manuals behind
// the Help and Support footer links are open to every signed-in user, and the general reports are listed only to those allowed
// them, by CDC.Api, so a signed-in user is let through here.
app.MapGet("/static-reports/versions/{staticReportVersionId:guid}/document", async (
        Guid staticReportVersionId,
        bool? download,
        IStaticReportDocumentApiService documents,
        HttpResponse response,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var document = await documents.GetDocumentAsync(staticReportVersionId, download ?? false, cancellationToken);

            if (document is null)
            {
                return Results.NotFound();
            }

            if (document.ContentDisposition is not null)
            {
                response.Headers.ContentDisposition = document.ContentDisposition;
            }

            return Results.File(document.Content, "application/pdf");
        }
        catch (HttpRequestException)
        {
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
    })
    .RequireAuthorization(CDC.Web.Authorization.Constants.AuthorizationPolicies.CanViewHelp);

// Ensure Razor Pages are available if any exist in the project
app.MapRazorPages();

await app.RunAsync();
