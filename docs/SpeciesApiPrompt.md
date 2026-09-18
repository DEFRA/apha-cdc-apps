# Species API (CDC.Api) — Implementation Prompt

## Original prompt

```
You are assisting with the modernization of a legacy application called Surveillance Profiles.
This prompt is to develop code in CDC.Api project.

IMPORTANT:
- Follow all standards defined in copilot-instructions.md.
- Generate production-ready code only.
- Use .NET 8.
- Use Clean Architecture.
- Use SOLID principles.
- Use async/await throughout.
- Enable nullable reference types.
- Use MediatR, FluentValidation, Dapper, Swagger/OpenAPI, xUnit, Moq, FluentAssertions, AutoFixture.
- Generate complete code, not placeholders.
- Include XML comments for public members.
- Include structured logging.
- Return RFC7807 ProblemDetails for errors.

PROJECT CONTEXT

Legacy system: ASP.NET WebForms, .NET Framework 4.8, VB.NET + C#, WCF, CSLA.NET, SQL Server,
ADO.NET, NPoco.

Target architecture: ASP.NET Core Web API, Clean Architecture, Dependency Injection, Dapper
repositories, Swagger/OpenAPI, Unit Tests.

First migrated service: ISpeciesDataService
  Legacy WCF implementation: Profiles.Services\SpeciesDataService.svc.vb
  Legacy repository:         Profiles.DataAccess.Sql\SpeciesDataService.vb

  Methods: GetAllSpecies, GetSpeciesMetadata, GetSpeciesAnswerData, UpdateSpeciesAnswerData,
           GetAllSelectedSpecies

  Stored procedures: spgaSpecies, spgaSpeciesSectionMetadata, and any used by
  UpdateSpeciesAnswerData.

  Business responsibilities: retrieve all species; retrieve species metadata; retrieve selected
  species; retrieve species answer data; update species answer data; support transactional
  changeset updates; preserve existing behavior.

DELIVERABLES

Solution structure (src/SurveillanceProfiles.Api, .Application, .Domain, .Infrastructure and
tests/SurveillanceProfiles.UnitTests), domain entities, DTOs, repository interface and
implementation, service layer, MediatR queries and commands with handlers, FluentValidation
validators, dependency injection extensions, global exception middleware, Swagger/OpenAPI
configuration, SpeciesController, unit tests, appsettings.json, README.md.

API endpoints:
  GET /api/species                              -> 200
  GET /api/species/metadata                     -> 200
  GET /api/species/{profileVersionId}/answers   -> 200, 404
  GET /api/species/{profileVersionId}/selected  -> 200
  PUT /api/species/answers                      -> 200, 400, 409

Unit test coverage: SpeciesController (7 named tests), SpeciesService (5 named tests),
SpeciesRepository (mock database interactions; verify stored procedure execution, transaction
rollback, exception handling, logging on failure), Validator (4 named tests).

Save the prompt in new files in docs.
```

## Decisions taken before implementation

Three points in the brief conflicted with the repository or with the legacy contract. Each was
confirmed with the requester before any code was written.

### 1. Code location — inside `CDC.Api`, not four new projects

The brief opened with "this prompt is to develop code in CDC.Api project" and later asked for
four `SurveillanceProfiles.*` projects. **Chosen: Clean Architecture as folders inside the
existing `CDC.Api` project.**

The layering and dependency direction are enforced by namespace and by which types reference
which — `Domain` references nothing, `Application` defines `ISpeciesRepository` and
`Infrastructure` implements it. Splitting into four assemblies would have added three csproj
files, a solution change and a Docker build change for a single feature slice, without changing
the design.

### 2. Target framework — `net10.0`, not `net8.0`

`Directory.Build.props`, `CDC.Api.csproj`, `CDC.Web.csproj` and `CDC.Api.Tests.csproj` all
target `net10.0`, and `.github/copilot-instructions.md` mandates .NET 10. Building this feature
against .NET 8 would have required downgrading the whole repository.

### 3. Route keys — `speciesId` and `diseaseName`, not `profileVersionId`

The brief specified `profileVersionId` in two routes. The legacy stored procedures do not
support it:

- `spgSpeciesAnswerData` takes `@SpeciesId uniqueidentifier`.
- `spuSpeciesAnswerData`, `spuSpeciesFieldValue`, `spdSpeciesFieldMultiValue` and
  `spiSpeciesFieldMultiValue` all take `@SpeciesId`.
- `spgaSelectedDiseaseSpecies` takes `@DiseaseName nvarchar(500)` — the legacy
  `GetAllSelectedSpecies(request As String)` passes a disease name.

Since the brief also required "preserve existing behavior", the legacy contract won:

| Brief | Implemented |
|---|---|
| `GET /api/species/{profileVersionId}/answers` | `GET /api/species/{speciesId}/answers` |
| `GET /api/species/{profileVersionId}/selected` | `GET /api/species/selected?diseaseName=` |
| `PUT /api/species/answers` with `{ profileVersionId, changes }` | `PUT /api/species/answers` with `{ speciesId, lastUpdated, changes }` |

`lastUpdated` was added because `spuSpeciesAnswerData` requires the `@LastUpdated` row version
and raises an error if it has moved on. Without it, optimistic concurrency — and therefore the
409 response the brief asks for — cannot work.

## Other deviations from the brief

| Brief | Implemented | Reason |
|---|---|---|
| Repository methods return DTOs | Repository returns domain entities; the service maps to DTOs | Clean Architecture, which the brief also mandates: the infrastructure layer must not own the API contract |
| `UpdateSpeciesAnswerData` as in the legacy WCF service | Fully implemented | The legacy WCF method threw `InvalidOperationException("You cannot update the species answer data via the web service.")`; the real logic lived in `Profiles.DataAccess.Sql`, and that is what was migrated |
| `ParameterName` strings (`"@BooleanValue"`) on each change | Typed `SpeciesFieldValueKind` enum | Type safety; the repository still sets exactly the same stored procedure parameters |
| Swagger "request examples / response examples" via filters | Sample payloads in XML `<remarks>`, rendered by Swashbuckle as the operation description | Swashbuckle 10 moved to OpenAPI.NET v2, where schema examples are `JsonNode`; XML samples achieve the same outcome without a fragile filter |
| MediatR and FluentAssertions latest | MediatR 12.5.0, FluentAssertions 7.2.0 | MediatR 13+ and FluentAssertions 8+ require paid commercial licences; these are the last Apache-2.0 releases |
| `ConnectionStrings` section in `appsettings.json` | Present, but documented as unused | `SqlConnectionFactory` composes the connection string from `Database:*` values injected from Parameter Store and Secrets Manager. A literal connection string in configuration would breach the repository's secret-handling rules |

## Implementation notes

### Stored procedures with duplicate column names

`spgaSpeciesSectionMetadata` result set 3 returns `Id` twice (question then field), and
`spgSpeciesAnswerData` result set 3 does the same; result set 1 of the latter also returns an
unnamed column. Dapper maps by column name and cannot disambiguate these, so those two
procedures are read positionally through a `DbDataReader` — exactly as the legacy
`SafeDataReader` code did. The other procedures return uniquely named columns and are mapped by
Dapper.

### Missing `EditorFieldType` column

The legacy VB code reads `EditorFieldType` at ordinal 11, but the create script for
`spgaSpeciesSectionMetadata` only returns 11 columns (0–10), which would throw. The repository
reads ordinal 11 only when the reader actually has it, and defaults to `0` otherwise, so it
works against both the scripted and any later version of the procedure.

### Testing a Dapper repository

Dapper extends `DbConnection`, not an interface, so it cannot be mocked with Moq. The test
project contains a small in-memory ADO.NET provider (`Fakes/FakeDb.cs`: connection, command,
parameter collection, data reader, transaction) that records every command. This allows real
assertions on stored procedure names, parameter sets, execution order, transaction commit and
rollback, and error logging.

## Files created

- `src/CDC.Api/Domain/Common/BaseEntity.cs`, `Result.cs`
- `src/CDC.Api/Domain/Entities/*.cs` — 9 entities
- `src/CDC.Api/Domain/Exceptions/NotFoundException.cs`, `ConcurrencyException.cs`
- `src/CDC.Api/Application/ApplicationDependencyInjection.cs`
- `src/CDC.Api/Application/Behaviours/ValidationBehaviour.cs`
- `src/CDC.Api/Application/Extensions/ResultExtensions.cs`
- `src/CDC.Api/Features/Species/SpeciesController.cs`, `SpeciesService.cs`, `SpeciesLog.cs`
- `src/CDC.Api/Features/Species/Commands/*.cs` — command, handler, validator
- `src/CDC.Api/Features/Species/Queries/*.cs` — four queries with handlers
- `src/CDC.Api/Features/Species/Dtos/*.cs`, `Interfaces/*.cs`, `Mapping/SpeciesMappings.cs`
- `src/CDC.Api/Infrastructure/InfrastructureDependencyInjection.cs`
- `src/CDC.Api/Infrastructure/Repositories/SpeciesRepository.cs`, `SpeciesStoredProcedures.cs`
- `src/CDC.Api/Infrastructure/Swagger/SwaggerConfiguration.cs`
- `src/CDC.Api/Middleware/GlobalExceptionMiddleware.cs`
- `src/CDC.Api/README.md`
- `tests/CDC.Api.Tests/Fakes/FakeDb.cs`
- `tests/CDC.Api.Tests/Species/*.cs` — test data builders, controller, service, handler,
  repository and validator tests
- `tests/CDC.Api.Tests/Application/ValidationBehaviourTests.cs`
- `tests/CDC.Api.Tests/Middleware/GlobalExceptionMiddlewareTests.cs`

## Files modified

- `src/CDC.Api/Program.cs` — controllers, Swagger, exception middleware, layer registration
- `src/CDC.Api/CDC.Api.csproj` — MediatR, FluentValidation, Swashbuckle, `GenerateDocumentationFile`
- `src/CDC.Api/appsettings.json` — `ConnectionStrings`, `Swagger`, `SmtpSettings`, `SsoSettings`
  sections, all with placeholder values only
- `tests/CDC.Api.Tests/CDC.Api.Tests.csproj` — Moq, FluentAssertions, AutoFixture
- Existing `Features/Health` and `Infrastructure` files — XML documentation added, required once
  `GenerateDocumentationFile` was enabled

## Testing performed

- `dotnet build src\CDC.Api\CDC.Api.csproj` — 0 errors, 0 warnings.
- `dotnet test tests\CDC.Api.Tests\CDC.Api.Tests.csproj` — 75 passed, 0 failed, 0 skipped.
- Coverage: 93.24% line, 77.5% branch, 98.13% method, against the repository's 80% line
  threshold.
