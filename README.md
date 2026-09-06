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

`CDC.Web` uses a feature-folder convention: each feature lives under `Features/<Name>/` with its own
controller and a `Views/` subfolder, instead of the default flat `Controllers/`+`Views/` split.

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
