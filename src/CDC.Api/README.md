# Surveillance Profiles API (CDC.Api)

ASP.NET Core Web API for the Surveillance Profiles (D2R2) modernisation. The first migrated
service is **Species data**, replacing the legacy `ISpeciesDataService` WCF endpoint
(`Profiles.Services\SpeciesDataService.svc.vb` and `Profiles.DataAccess.Sql\SpeciesDataService.vb`).

## Architecture

Clean Architecture, implemented as folders inside a single deployable project. Dependencies
point inwards only.

```
Api (controllers, middleware, Program.cs)
  └─> Application (MediatR handlers, validators, services, DTOs)
        └─> Domain (entities, Result, exceptions)

Infrastructure (Dapper repositories, connection factory, Swagger)
  └─> Application  (implements Features/Species/Interfaces)
        └─> Domain
```

- **Domain** has no dependency on any other layer or framework package.
- **Application** defines `ISpeciesRepository`; **Infrastructure** implements it. The dependency
  is inverted through DI, so the application layer never references Dapper or SQL Server.
- **CQRS** via MediatR: one request type per use case, each with its own handler.
- **Validation** runs in a MediatR pipeline behaviour before any handler executes.
- **Result pattern** for expected outcomes (not found, conflict); exceptions only for faults.

### Project structure

| Path | Contents |
|---|---|
| `Domain/Common` | `BaseEntity`, `Result<T>` |
| `Domain/Entities` | `Species`, `SelectedSpecies`, `SpeciesMetadata`, `SpeciesSectionMetadata`, `SpeciesQuestionMetadata`, `SpeciesFieldMetadata`, `SpeciesAnswerData`, `SpeciesSection`, `SpeciesFieldValue` |
| `Domain/Exceptions` | `NotFoundException`, `ConcurrencyException` |
| `Application` | `ApplicationDependencyInjection`, `Behaviours/ValidationBehaviour`, `Extensions/ResultExtensions` |
| `Features/Species` | `SpeciesController`, `SpeciesService`, `SpeciesLog` |
| `Features/Species/Commands` | `UpdateSpeciesAnswerDataCommand`, its handler and validator |
| `Features/Species/Queries` | `GetAllSpeciesQuery`, `GetSpeciesMetadataQuery`, `GetSpeciesAnswerDataQuery`, `GetAllSelectedSpeciesQuery` |
| `Features/Species/Dtos` | Response contracts |
| `Features/Species/Interfaces` | `ISpeciesRepository`, `ISpeciesService` |
| `Features/Species/Mapping` | Entity to DTO projections |
| `Infrastructure` | `InfrastructureDependencyInjection`, `SqlConnectionFactory`, `StartupChecks` |
| `Infrastructure/Repositories` | `SpeciesRepository`, `SpeciesStoredProcedures` |
| `Infrastructure/Swagger` | `SwaggerConfiguration` |
| `Middleware` | `GlobalExceptionMiddleware` |
| `Features/Health` | Liveness and key-gated readiness endpoints |

## Setup

Prerequisites: .NET 10 SDK, and network access to a Surveillance Profiles SQL Server database.

```powershell
dotnet restore
dotnet build src\CDC.Api\CDC.Api.csproj
```

### Database configuration

No connection string is stored anywhere. `SqlConnectionFactory` composes one at runtime from
four separate values, validated at startup by `StartupChecks`:

| Key | Environment variable | Source in AWS |
|---|---|---|
| `Database:Host` | `Database__Host` | Parameter Store |
| `Database:Name` | `Database__Name` | Parameter Store |
| `Database:User` | `Database__User` | Parameter Store |
| `Database:Password` | `Database__Password` | Secrets Manager |
| `Database:TrustServerCertificate` | `Database__TrustServerCertificate` | Local development only; leave `false` in deployed environments |
| `HealthCheck:ReadinessKey` | `HealthCheck__ReadinessKey` | Secrets Manager |

Locally, put them in `appsettings.Development.json` — except the password, which must go in user
secrets so that it is never committed:

```powershell
dotnet user-secrets --project src\CDC.Api\CDC.Api.csproj set "Database:Password" "<password>"
```

The application fails fast at startup if any of these are missing.

Against SQL Server LocalDB, `Database:Host` is `(localdb)\MSSQLLocalDB` and
`Database:TrustServerCertificate` must be `true`, because LocalDB presents a self-signed
certificate. Leave it `false` everywhere else.

The stored procedures the species feature calls must exist in the target database:
`spgaSpecies`, `spgaSelectedDiseaseSpecies`, `spgaSpeciesSectionMetadata`, `spgSpeciesAnswerData`,
`spuSpeciesAnswerData`, `spuSpeciesFieldValue`, `spdSpeciesFieldMultiValue`,
`spiSpeciesFieldMultiValue`, `sppSpeciesPrioritisationScore`. They are unchanged from the legacy
system - the create scripts live in `D2R2-2026-08-17\ProfilesDatabase\Create Scripts\Stored Procedures`.

### Running locally

```powershell
dotnet run --project src\CDC.Api\CDC.Api.csproj
```

Swagger UI: `https://localhost:{port}/swagger`
OpenAPI v3 document: `https://localhost:{port}/swagger/v1/swagger.json`

Set `Swagger:Enabled` to `false` to turn the UI off without a code change.

### Running the tests

```powershell
dotnet test tests\CDC.Api.Tests\CDC.Api.Tests.csproj
```

xUnit, Moq, FluentAssertions and AutoFixture. Coverlet enforces a 90% line coverage threshold
(`tests\Directory.Build.props`). The repository is tested against an in-memory fake ADO.NET
provider (`tests\CDC.Api.Tests\Fakes\FakeDb.cs`), because Dapper extends `DbConnection` rather
than an interface - this verifies stored procedure names, parameters, transaction commit and
rollback without a live SQL Server.

## API

Base route `/api/species`. All responses are JSON; errors are RFC 7807 `application/problem+json`.

| Method | Route | Purpose | Responses |
|---|---|---|---|
| GET | `/api/species` | All species and species groups | 200 |
| GET | `/api/species/metadata` | Questionnaire structure | 200 |
| GET | `/api/species/{speciesId}/answers` | Recorded answers for one species | 200, 404 |
| GET | `/api/species/selected?diseaseName=` | Species selected for a disease filter | 200, 400 |
| PUT | `/api/species/answers` | Update answers transactionally | 200, 400, 409 |

### Examples

Get all species:

```http
GET /api/species
```

```json
[
  {
    "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    "parentId": "00000000-0000-0000-0000-000000000000",
    "description": "Cattle",
    "isActive": true,
    "isInUse": true
  }
]
```

Update answers. `lastUpdated` is the row version returned by the answers endpoint, base64
encoded. `kind` selects the value: `0` clear, `1` boolean, `2` list, `3` text, `4` multi-select.

```http
PUT /api/species/answers
Content-Type: application/json

{
  "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
  "lastUpdated": "AAAAAAAAB9E=",
  "changes": [
    { "fieldId": "0c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f", "kind": 1, "booleanValue": true },
    { "fieldId": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071", "kind": 4, "multiValues": ["3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182"] }
  ]
}
```

```json
{
  "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
  "lastUpdated": "AAAAAAAAB9I="
}
```

Conflict response when another user has saved first:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflicting change",
  "status": 409,
  "detail": "Species '6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f' has been edited by another user. Re-read the answer data and try again.",
  "instance": "/api/species/answers",
  "traceId": "00-2f1c..."
}
```

## Behaviour preserved from the legacy service

- Answer data is keyed on **SpeciesId**, and `GetAllSelectedSpecies` filters on a **disease
  name**, exactly as the stored procedures require.
- Optimistic concurrency uses the `Species.LastUpdated` row version. `spuSpeciesAnswerData`
  raises an error when it no longer matches; that becomes HTTP 409 and nothing is written.
- An update runs the row version check, every field change, and the prioritisation score
  recalculation inside one transaction, matching the legacy `TransactionScope`.
- A multi-value change deletes every stored value for the field and re-inserts the supplied
  list, as the legacy `UpdateMultiFieldValue` did.
- The species `Name` column is exposed as `description`, matching the legacy data contract.

Deliberate differences, all recorded in `docs\SpeciesApiPrompt.md`:

- The legacy WCF `UpdateSpeciesAnswerData` threw `InvalidOperationException`; updates are now
  supported through the API, using the same stored procedures the CSLA layer called.
- Field values that are NULL in the database are returned as `null` rather than coerced to
  `false` / `Guid.Empty` / `""` by `SafeDataReader`.
- The legacy `ParameterName` strings (`"@BooleanValue"`) are replaced by a typed `kind` enum
  that drives the same stored procedure parameters.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `Database:Host, Database:Name, Database:User and Database:Password must all be configured` at startup | Missing configuration. Locally set the password with `dotnet user-secrets` and the rest in `appsettings.Development.json`; in AWS check the `Database__*` wiring in the ECS task definition. |
| `A network-related or instance-specific error occurred while establishing a connection to SQL Server` | `Database:Host` does not match the running instance. For LocalDB use `(localdb)\MSSQLLocalDB` and confirm it is started with `sqllocaldb info MSSQLLocalDB`. |
| `The certificate chain was issued by an authority that is not trusted` | Encryption is on by default in Microsoft.Data.SqlClient. Set `Database:TrustServerCertificate` to `true` for LocalDB or a self-signed development server only. |
| `Login failed for user 'SurveillanceProfilesAppUser'` | The SQL login is missing, has a different password, or the instance is not in mixed-authentication mode. Re-set the user secret and confirm the login exists on that instance. |
| `HealthCheck:ReadinessKey is not configured` at startup | Set `HealthCheck__ReadinessKey`. The readiness endpoint cannot distinguish "unset" from "wrong key" at request time, so this is validated at startup instead. |
| `/health/ready` returns 404 | The `X-Readiness-Key` header is missing or wrong. This is deliberate: an unauthenticated caller must not be able to tell the endpoint exists. |
| 500 with `"title": "Database error"` | The stored procedure failed. The detail is suppressed outside Development; search the logs for event 1009 (`StoredProcedureFailed`) and the trace id from the response. |
| 409 on every update | The client is sending a stale `lastUpdated`. Re-read `GET /api/species/{speciesId}/answers` and resend with the returned row version. |
| Swagger UI is empty or missing summaries | `GenerateDocumentationFile` must be enabled and `CDC.Api.xml` must be next to the DLL. It is copied to the output folder automatically; check the Docker image includes it. |
| `SpeciesRepository requires a DbConnection so that database calls can be awaited` | `IDbConnectionFactory` was replaced with an implementation that does not return a `DbConnection`. Async ADO.NET is only available on `DbConnection`. |
