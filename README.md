# D2R2 (Disease Briefing, Decisions Support, Risk Assessment & Ranking)

D2R2 is an internal APHA (Animal and Plant Health Agency, part of Defra) web application used to author,
review, version-control and publish structured animal disease surveillance profiles. Each profile records
how a disease is monitored, ranked by risk, and reviewed over time, and is used by veterinary and policy
professionals both inside APHA (internal users) and outside it (external contributors/reviewers).

This repo is the re-platform of the legacy `SurveillanceProfiles` (ASP.NET Web Forms) application onto
modern .NET, running on AWS ECS Fargate.

## Tech stack

- .NET 10 / ASP.NET Core MVC (Razor views)
- Single portal for both internal (Microsoft Entra ID, future work) and external (GOV.UK One Login via
  DEFRA CIDM / OIDC, implemented) users. External users sign in via the CIDM OIDC integration (see
  "CIDM authentication" section below); the internal-user path remains a placeholder pending Entra ID
  integration.

## Project structure

```
CDC.slnx                  # solution file
src/
  CDC.Web/                 # presentation layer - MVC controllers, Razor views, wwwroot
  CDC.Api/                 # business logic + database access (REST API, called by CDC.Web)
```

Both projects use the same feature-folder convention: feature-specific code lives under `Features/<Name>/`
(in `CDC.Web`, each feature folder has its own controller and `Views/` subfolder; in `CDC.Api`, its own
Minimal API endpoint-mapping class), instead of the default flat `Controllers/`/`Endpoints/` split.
Cross-cutting concerns that aren't tied to one feature (the typed `HttpClient` in `CDC.Web`, the DB
connection factory and startup checks in `CDC.Api`) live under `Infrastructure/` instead:

```
src/CDC.Api/
  Features/
    Health/              # HealthEndpoints, DatabaseHealthCheck, ReadinessKeyFilter
  Infrastructure/         # StartupChecks, IDbConnectionFactory, SqlConnectionFactory
  Program.cs
```

## Getting started

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
# restore local dev tools (Husky.Net, used for the pre-commit hook)
dotnet tool restore

# build everything
dotnet build CDC.slnx

# run the web app
dotnet run --project src/CDC.Web/CDC.Web.csproj
```

## CIDM authentication (CDC.Web - external users)

External users sign in via [DEFRA Customer Identity (CIDM)](https://www.gov.uk/using-your-gov-uk-one-login),
using OpenID Connect. Internal users sign in via Microsoft Entra ID.

Configuration is supplied via `IConfiguration` - never hardcoded or committed to source control. Both
`CDC.Web`'s CIDM and Entra ID options are validated on startup (`ValidateOnStart`), so the app will refuse
to start at all - with an `OptionsValidationException` listing every missing key - until all of the below
are set:

| Config key           | Purpose                                    |
| -------------------- | ------------------------------------------- |
| `Cidm:Address`       | CIDM/IdP Hub base address                   |
| `Cidm:Policy`        | B2C policy name for the target environment  |
| `Cidm:ClientId`      | OIDC client ID                              |
| `Cidm:ClientSecret`  | OIDC client secret                          |
| `Cidm:ServiceId`     | DEFRA-specific `serviceId` authorize param  |
| `Entra:TenantId`     | Entra ID tenant (directory) ID              |
| `Entra:ClientId`     | Entra ID app registration client ID         |
| `Entra:ClientSecret` | Entra ID app registration client secret     |

For local development, set them via `dotnet user-secrets`. If you don't have real CIDM/Entra ID app
registrations to hand, any non-empty placeholder values are enough to satisfy startup validation and run
the app locally - sign-in itself just won't complete against a real identity provider:

```powershell
cd src/CDC.Web
dotnet user-secrets set "Cidm:Address" "https://<cidm-tenant>.b2clogin.com/<tenant>.onmicrosoft.com"
dotnet user-secrets set "Cidm:Policy" "<policy-name>"
dotnet user-secrets set "Cidm:ClientId" "<client-id>"
dotnet user-secrets set "Cidm:ClientSecret" "<client-secret>"
dotnet user-secrets set "Cidm:ServiceId" "<service-id>"
dotnet user-secrets set "Entra:TenantId" "<tenant-id>"
dotnet user-secrets set "Entra:ClientId" "<client-id>"
dotnet user-secrets set "Entra:ClientSecret" "<client-secret>"
```

## Session timeout (CDC.Web)

A signed-in session - an internal (Entra ID) or an external (CIDM) user's alike - ends after a period with no activity, as
the legacy application's 20-minute session did. Any request to CDC.Web is activity and renews the session (the sign-in
cookie is re-issued at most once every twentieth of the timeout); asking how long a session has left is not, so it can never
keep an idle session alive. Five minutes before a session ends, the user is asked "Extend session?"; once it has ended, a
"Session expired" dialog sends them back through sign-in to the page they were on. The dialogs are built into every page
that has a signed-in user (`_SessionTimeout.cshtml`, `wwwroot/js/session-timeout.js`, `SessionController`).

Both settings are optional - the defaults reproduce the legacy behaviour - and are validated on startup, so the app will not
start with values that would end a session without a user being able to respond to the warning:

| Config key                           | Default    | Purpose                                                                                                             |
| ------------------------------------ | ---------- | ------------------------------------------------------------------------------------------------------------------- |
| `SessionTimeout:IdleTimeout`         | `00:20:00` | How long a session may sit idle before the user has to sign in again (the lifetime of both sign-in cookies)         |
| `SessionTimeout:WarningBeforeExpiry` | `00:05:00` | How long before the session ends the user is asked whether to extend it: at least 20 seconds, shorter than the idle timeout |


## Code style & pre-commit checks

Formatting and code-style rules are defined in `.editorconfig`; Roslyn analyzers are enabled for every
project via `Directory.Build.props`. A Husky.Net git hook runs `dotnet format --verify-no-changes` against
staged `.cs` files before each commit - if it fails, run `dotnet format` to auto-fix, then re-stage and
commit again.

## Logging & correlation IDs

Both `CDC.Web` and `CDC.Api` log structured JSON to the console only (no file sinks) via Serilog, using
`CompactJsonFormatter`. Locally, that means one JSON object per line in the terminal running `dotnet run`;
in a deployed environment, the same stdout stream is what the container's log driver ships onward - just
follow whatever your environment tails (e.g. `docker logs -f <container>` locally, or the container
platform's own log viewer/tail command elsewhere).

Each log line includes `MachineName`, `EnvironmentName`, and - for anything logged during a request -
`CorrelationId`. To follow a single request end-to-end, filter/grep the log stream for its `CorrelationId`
value.

**Correlation IDs are automatic, not manual.** `CorrelationIdMiddlewareExtensions.UseCorrelationId()`
(in `CDC.Common`, registered in both `Program.cs` files before `UseSerilogRequestLogging()`) reads the
`X-Correlation-Id` request header; if it is missing or not a well-formed GUID, a new one is generated. No
caller is required to supply one, but a caller (e.g. an upstream service, or a manual `curl`/Postman
request) can supply their own GUID to make a request traceable under a known value. The ID is:

- pushed into the Serilog `LogContext` for the lifetime of the request, so every log line carries it,
- echoed back on the response's `X-Correlation-Id` header, and
- forwarded automatically from `CDC.Web` to `CDC.Api` by `CorrelationIdDelegatingHandler`, attached to the
  typed `HttpClient` used for `IApiClient` - so one user action produces the same `CorrelationId` in both
  services' logs.

### Example: logging from application code

Inject `ILogger<T>` and log with a structured message template - never string interpolation - so the
values stay queryable as real JSON fields rather than being flattened into the message text:

```csharp
public sealed class SpeciesService(ISpeciesRepository repository, ILogger<SpeciesService> logger)
{
    public async Task<Result<Species>> UpdateAnswerDataAsync(Guid speciesId, SpeciesAnswerData data)
    {
        logger.LogInformation("Updating species {SpeciesId} with {FieldCount} answered fields", speciesId, data.Fields.Count);

        var result = await repository.UpdateAnswerDataAsync(speciesId, data);

        if (result.IsFailure)
        {
            logger.LogWarning("Species {SpeciesId} update rejected: {Reason}", speciesId, result.Error);
        }

        return result;
    }
}
```

Because `CorrelationId` is already in the Serilog `LogContext` for the current request (see above), both
log lines above are automatically enriched with it - no need to pass it around manually. The resulting
CloudWatch Logs Insights query to see everything that happened for one update, across both log lines:

```
fields @timestamp, CorrelationId, SpeciesId, @message
| filter SpeciesId = "…"
| sort @timestamp asc
```

