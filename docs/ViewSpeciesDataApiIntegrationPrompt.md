# ViewSpeciesData — Load Species Data from CDC.Api

## Original prompt

```
Use copilot-instructions.md and follow the existing project architecture, CQRS pattern, Npoco
data access pattern, Razor Pages conventions, and coding standards.

Requirement:
Implement loading and displaying Species data in the ViewSpeciesData Razor Page.

API Endpoint:
GET /api/species

Sample API Response:
[
  {
    "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    "parentId": "00000000-0000-0000-0000-000000000000",
    "description": "Cattle",
    "isActive": true,
    "isInUse": true
  }
]

Tasks:
1. Create a strongly typed Species DTO/model matching the API response.
2. Create a typed HTTP client/service to call GET /api/species.
3. Read the API base URL from appsettings.json.
4. Implement proper error handling and logging.
5. In ViewSpeciesData.cshtml.cs: call the Species API from OnGetAsync(), retrieve all species
   records, store the results in a page model property, only display active records
   (IsActive = true).
6. Convert the flat API response into a hierarchical tree structure using id/parentId, with
   00000000-0000-0000-0000-000000000000 as the root marker, supporting unlimited child levels
   using recursion.
7. Bind the hierarchical data to the existing TreeView component, preserving expand/collapse.
8. Display Description as the node text.
9. Add loading and error states: "Loading species data..." while retrieving, a user-friendly
   message on failure, and log exceptions.
10. Async/await throughout, SOLID, Dependency Injection, no hardcoded URLs, nullable reference
    types respected.

Expected deliverables: SpeciesDto.cs, ISpeciesApiService.cs, SpeciesApiService.cs, DI
registration updates, ViewSpeciesData.cshtml.cs updates, tree node mapping logic.

Save the prompt in new file under docs.
```

## Deviations from the brief, and why

### 1. "CQRS pattern" and "NPoco data access pattern" do not apply here

CDC.Web is a Razor Pages front end that talks to CDC.Api over HTTP — it has no database
connection and no MediatR pipeline of its own (those live in CDC.Api; see
`docs/SpeciesApiPrompt.md`). CQRS/MediatR and NPoco are data-access-layer concerns that belong
to the API, not to a page model calling a REST endpoint. This change follows the pattern CDC.Web
already uses for exactly this situation — a typed `HttpClient` service registered in
`Program.cs` (see `Infrastructure/ApiClient.cs` / `IApiClient`, used from
`ApiConnectivityHealthCheck`) — rather than introducing an unrelated architecture into the
project.

### 2. No true "loading" state on the initial request

Razor Pages with `OnGetAsync` render synchronously: the browser gets one response, containing
either the finished tree, an empty-state message, or an error — there is no intermediate
server-rendered "loading" response to show. A `<div>Loading species data...</div>` that flashes
for a single frame before the real response arrives would need a client-side fetch (turning the
page into a small SPA), which is out of scope for this change and inconsistent with every other
page in the project. The `ViewSpeciesData.cshtml` markup documents this with a comment; the
loading requirement is satisfied to the extent that is meaningful for a server-rendered page.

## What was implemented

### Domain

- **`Models/SpeciesDto.cs`** — matches the API response exactly: `Id`, `ParentId`,
  `Description`, `IsActive`, `IsInUse`.
- **`Models/SpeciesTreeBuilder.cs`** — pure mapping from the flat `SpeciesDto` list to the
  existing `TreeNodeViewModel` hierarchy (already used by the tree-picker UI). Filters to
  `IsActive` species, groups children by `ParentId` via `ILookup`, and recurses to any depth.
  Root nodes (`ParentId == Guid.Empty`) come back `Expanded = true`; everything below that starts
  collapsed. A species whose parent is missing or inactive is promoted to a root instead of
  being silently dropped, and a `HashSet` of ancestor ids guards against a cyclic `ParentId`
  chain causing unbounded recursion.

### Infrastructure

- **`Infrastructure/ISpeciesApiService.cs`** / **`SpeciesApiService.cs`** — typed client calling
  `GET /api/species` via `HttpClient.GetFromJsonAsync`. The base address is set once in
  `Program.cs` from the existing `Api:BaseUrl` configuration value (already used by `IApiClient`)
  — nothing is hardcoded, and no new configuration section was needed.
- **`Program.cs`** — registers `AddHttpClient<ISpeciesApiService, SpeciesApiService>` with the
  same resilience handler and base address as the existing `IApiClient` registration.

### Page

- **`Pages/ViewSpeciesData.cshtml.cs`** — `ISpeciesApiService` and `ILogger` injected via a
  primary constructor. `OnGetAsync` calls the service, builds the tree with
  `SpeciesTreeBuilder.Build`, and exposes `SpeciesTree`, `HasError` and `ErrorMessage`. Only
  `HttpRequestException`, `TaskCanceledException` (the resilience handler's own timeout) and
  `NotSupportedException` (malformed JSON) are caught — an unexpected exception still surfaces
  normally. Every failure is logged with a source-generated `LoggerMessage`
  (`SpeciesLoadFailed`, event id 2000).
- **`Pages/ViewSpeciesData.cshtml`** — renders one of three states: a GOV.UK error summary when
  `HasError`, a plain "No active species data is available." message when the tree is empty, or
  the existing tree-picker form. The tree-picker markup itself, the `_TreeView` partial and
  `tree-view.js` are unchanged — real data flows through the same component the static
  placeholder used.

### Tests (`tests/CDC.Web.Tests`)

Following the project's existing test style (plain xUnit + hand-written fakes, no Moq — see
`Features/Landing/FakeApiClient.cs`):

- **`Models/SpeciesTreeBuilderTests.cs`** — nesting to unlimited depth, inactive species
  excluded, only root nodes expanded, an orphaned species promoted to root, empty input.
- **`Infrastructure/SpeciesApiServiceTests.cs`** — deserialises a real JSON response via a fake
  `HttpMessageHandler`, returns an empty list for a `null` body, throws `HttpRequestException`
  on a non-success status code.
- **`Pages/ViewSpeciesDataModelTests.cs`** (+ `FakeSpeciesApiService.cs`) — builds the tree from
  active species only, sets `HasError`/`ErrorMessage` when the service throws, and produces an
  empty tree when nothing is active.

## Testing performed

- `dotnet build src\CDC.Web\CDC.Web.csproj` — 0 errors, 0 warnings.
- `dotnet test tests\CDC.Web.Tests\CDC.Web.Tests.csproj` — 43 passed, 0 failed; 90.64% line
  coverage against the project's 80% threshold.
- End-to-end: with CDC.Api unavailable (no `Database:Password` user secret configured in this
  session), `GET /ViewSpeciesData` returned HTTP 200 with the GOV.UK error summary ("There is a
  problem") and the API failure was logged as `CDC.Web.Pages.ViewSpeciesDataModel[2000] Failed
  to load species data from CDC.Api` — confirming the error path end-to-end. The happy path
  (real data through the tree) is covered by the unit tests above; it was not re-verified against
  a live database in this session because no `Database:Password` secret was available to start
  CDC.Api.
