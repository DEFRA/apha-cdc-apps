# D2R2 (Disease Briefing, Decisions Support, Risk Assessment & Ranking)

D2R2 is an internal APHA (Animal and Plant Health Agency, part of Defra) web application used to author,
review, version-control and publish structured animal disease surveillance profiles. Each profile records
how a disease is monitored, ranked by risk, and reviewed over time, and is used by veterinary and policy
professionals both inside APHA (internal users) and outside it (external contributors/reviewers).

This repo is the re-platform of the legacy `SurveillanceProfiles` (ASP.NET Web Forms) application onto
modern .NET, running on AWS ECS Fargate.

## Tech stack

- .NET 10 / ASP.NET Core MVC (Razor views)
- Single portal for both internal (Microsoft Entra ID / SAML) and external (GOV.UK One Login via CIDM /
  OIDC) users - authentication is not yet implemented; the current landing page is a placeholder that lets
  a user pick "Internal" or "External".

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
