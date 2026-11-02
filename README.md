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

## Review-due email notifications (CDC.Api)

A scheduled job emails each subscribed user one reminder listing the profile sections due for review
(ported from the legacy `ReviewEmailService`). Email is sent through [GOV.UK Notify](https://www.notifications.service.gov.uk)
by `NotifyEmailSender`. The job runs from the same image as the API: passing `review-notifications` as the
first argument runs it once and exits instead of starting Kestrel. In AWS it runs as an EventBridge
Scheduler-triggered ECS Scheduled Task. Code lives in `src/CDC.Api/Features/ReviewNotifications/`.

Users are selected by the database views `vwReviewEmailUsers`, `vwReviewEmailProfiles` and
`vwReviewEmailProfileSections`, which apply the two-week reminder window and the subscription, Draft-state
and not-Complete filters. Each user's profile roles are then mapped to a reviewer group
(`Policy`, `AuthorTechnical`, `VeterinaryAdvisor`) via `ReviewNotifications:ReviewerGroups`.

### Configuration

Bound from `CDC.Api` configuration. Set secrets with `dotnet user-secrets` (or `Notify__ApiKey` etc. as
environment variables); never commit them.

| Config key                              | Purpose                                                                                  |
| --------------------------------------- | ----------------------------------------------------------------------------------------- |
| `Notify:Enabled`                        | Must be `true`, otherwise every send throws and the job reports failure                  |
| `Notify:ApiKey`                         | Notify API key (`{name}-{service id}-{secret}`). Use a **team-only or test key** locally |
| `Notify:BaseUrl`                        | Defaults to `https://api.notifications.service.gov.uk`                                    |
| `Notify:TemplateId`                     | Template with `((name))`, `((subject))` and `((body))` personalisation                   |
| `ReviewNotifications:ReviewerGroups`    | Profile role names per reviewer group; a group with none configured is not emailed       |
| `ReviewNotifications:InternalHomePageUrl` / `ExternalHomePageUrl` | Links in the email; omitted when empty                      |
| `ReviewNotifications:SupportMailbox`    | Contact address shown in the email                                                       |
| `ReviewNotifications:TestRecipient`     | Testing only. When set, every reminder goes to this address instead of the user's own    |

The job also needs the `Database:*` settings described in `StartupChecks` (host, name, and user/password
or `Database:IntegratedSecurity` for LocalDB) and `HealthCheck:ReadinessKey`, as for the API itself.

> The `Notification` section in `src/CDC.Api/appsettings.json` does not match the `Notify` section the
> code binds to, so the keys in that file are currently ignored. Use the `Notify:*` keys above.

### Testing it locally

1. Create a Notify service (or ask the team for one), add a **team and guest list** or **test** API key, and
   create the email template(s) with the placeholders above.
2. Set the config:

   ```powershell
   cd src/CDC.Api
   dotnet user-secrets set "Notify:Enabled" "true"
   dotnet user-secrets set "Notify:ApiKey" "<team-only-or-test-key>"
   dotnet user-secrets set "Notify:TemplateId" "<template-id>"
   dotnet user-secrets set "ReviewNotifications:TestRecipient" "<your-email-address>"
   ```

   With `TestRecipient` set, every reminder is sent to that address instead of the user's own (the job logs a
   warning when it is active), so real users are never emailed. Leave it unset in deployed environments. A
   team-only Notify key additionally restricts delivery to addresses on your Notify team/guest list, so add
   your address there too.
3. Make sure the target database has a user who is subscribed to reminders and has a Draft profile section
   with a review date inside the two-week window, and a profile role listed under the matching
   `ReviewerGroups` entry.
4. Run the job once from the repo root:

   ```powershell
   dotnet run --project src/CDC.Api/CDC.Api.csproj -- review-notifications
   ```

5. Check the output. The log shows `ReminderSent` per user and a final `JobCompleted` with sent/failed
   counts. The process exit code is `0` when every due reminder was sent and `1` if any failed (also when a
   reviewer group is not configured). `echo $LASTEXITCODE` shows it in PowerShell. Check the recipient's
   inbox and the Notify dashboard for delivery status.

If no review is due the job logs that and sends nothing. Failure causes: a malformed `Notify:ApiKey`,
`Notify:Enabled` left `false`, or Notify rejecting the template or recipient (the log includes Notify's error
message but never the recipient address).

### Automated tests

These need no Notify key or database. Run from the repo root. The coverage gate (90% line) applies to the
whole suite, so a filtered run needs it switched off with `/p:Threshold=0` or it fails despite every test
passing:

```powershell
dotnet test tests/CDC.Api.Tests --filter "FullyQualifiedName~ReviewNotifications" /p:Threshold=0
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

