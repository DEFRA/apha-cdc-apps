# ViewSpeciesData Page — Implementation Prompt

## Original prompt

```
Follow all conventions, patterns, coding standards, project structure, and implementation guidance defined in copilot-instructions.md.

User Story:
When a user clicks the "View Species Data" hyperlink on the Internal page, they should be redirected to a new Razor page named "ViewSpeciesData".

Requirements:
1. Create a new Razor page:
   - Page name: ViewSpeciesData
   - Follow existing Razor page patterns used throughout the application.
   - Add navigation/routing consistent with the application's existing conventions.

2. Update the Internal page:
   - Locate the existing "View Species Data" hyperlink.
   - Configure the hyperlink to navigate to the new ViewSpeciesData page.

3. ViewSpeciesData page content:
   - Copy the same static text/content from the corresponding page in the legacy project:
     D2R2-2026-08-17
   - Preserve headings, labels, instructions, and layout structure exactly as they appear in the legacy implementation.
   - Apply the styling conventions used by the current application.

4. Telerik UI Tree component:
   - Reuse the same Telerik UI Tree component implementation and page layout pattern from the legacy project D2R2-2026-08-17.
   - Add the Telerik Tree control to the ViewSpeciesData page.
   - Create the Tree component structure and configuration only.
   - Do not implement data loading logic yet.
   - Configure the tree with placeholder/mock nodes if required for rendering.
   - Ensure the component is ready for a future data source implementation.

5. Scope limitations:
   - Do not implement API calls, repositories, services, or database access.
   - Do not add business logic for loading species data.
   - Only create the page navigation, static content, and Telerik Tree UI structure.

Acceptance Criteria:
- Clicking "View Species Data" from the Internal page navigates to /ViewSpeciesData.
- A new ViewSpeciesData Razor page exists.
- The page contains the same static content as the legacy D2R2-2026-08-17 implementation.
- The Telerik UI Tree component is displayed using the same UI pattern as the legacy application.
- The Tree component is prepared for future data binding but currently uses placeholder data or an empty data source.
- The solution builds successfully with no warnings or errors introduced by this change.

Before implementing:
- Review copilot-instructions.md.
- Review the existing View Species Data implementation in D2R2-2026-08-17.
- Reuse existing project patterns, naming conventions, dependency injection practices, Telerik component configuration, and Razor page structure.

Save prompt in new file in docs
```

## Important deviation: Telerik UI Tree component

**Telerik UI for ASP.NET Core is a commercial, license-key-gated NuGet package** hosted on
Telerik's private NuGet feed. It is **not installed, referenced, or licensed** anywhere in
`apha-cdc-apps` (the only package reference in `CDC.Web.csproj` prior to this change was
`Microsoft.Extensions.Http.Resilience`). Adding the real `Telerik.UI.for.AspNet.Core` package
would require:

- A Telerik license key.
- Access to Telerik's private NuGet feed (`nuget.telerik.com`), which is not configured in this
  workspace and cannot be added without credentials that are not available to this change.

Attempting to add the package reference without those would break `dotnet restore`/`dotnet build`
for everyone on the team, which conflicts with the acceptance criterion "the solution builds
successfully with no warnings or errors introduced by this change."

**What was implemented instead:** the same *UI pattern* as the legacy `RadTreeView` — a
toggle-all button (with the same `govuk-accordion-nav__chevron` icon class used by the legacy
`TreeViewToggleButton.ascx`), a bordered checkbox tree with mock/placeholder nodes, a "Selected: …"
summary line, a "Remove selection" button, and a "View data" button — using plain, semantic HTML
and a small vanilla-JS script (`wwwroot/js/species-tree.js`) for expand/collapse and selection
display only. No data loading, API calls, or business logic were added, per the scope
limitations.

If/when a Telerik UI for ASP.NET Core licence and NuGet feed access become available, this
placeholder tree can be swapped for the real `TelerikTreeView` component without changing the
surrounding page structure, route, or breadcrumb.

## Files created

- `src/CDC.Web/Pages/ViewSpeciesData.cshtml` — new top-level Razor Page, route `/ViewSpeciesData`.
- `src/CDC.Web/Pages/ViewSpeciesData.cshtml.cs` — `ViewSpeciesDataModel`, derives from the
  existing `BreadcrumbPageModelBase` (section "Species Data", page "View species data"), matching
  the pattern used by all other Razor Pages added previously in this project.
- `src/CDC.Web/wwwroot/js/species-tree.js` — toggle-all and selection-summary behaviour for the
  placeholder tree (no data access).

## Files modified

- `src/CDC.Web/Features/Landing/Views/Internal.cshtml` — the "View Species Data" link's
  `href="#"` was changed to `asp-page="/ViewSpeciesData"`.
- `src/CDC.Web/wwwroot/css/site.css` — added `.app-species-tree*` rules for the bordered tree
  container, toggle button/chevron, and checkbox tree layout.

## Content parity with the legacy page

The static introductory paragraph is copied verbatim from
`D2R2-2026-08-17/Profiles.Web/ViewSpeciesData.aspx`:

> "This page details the species that are used by the D2R2 system. It is presented as a
> hierarchical list that groups species into groups; a profile can reference an individual
> species or an entire species group. Click on any species or species group to view the data
> that is associated with that species or group."

The page title ("View species data"), the bordered tree container, the toggle-all control, the
"Selected: …" summary, the "Remove selection" button, and the "View data" button all mirror the
legacy layout (`ViewSpeciesData.aspx` + `TreeViewToggleButton.ascx` + `TreeSelection.ascx`).

## Testing performed

- `dotnet build src/CDC.Web/CDC.Web.csproj` — completed with **0 warnings, 0 errors**.
