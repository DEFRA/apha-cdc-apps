# Header Menu and Navigation Implementation

## 1. Original prompt

```
Analyze the existing D2R2-2026-08-17 application and implement the following enhancement in apha-cdc-apps.

Note : Make changes only in apha-cdc-apps repository.

Requirements:

1. Header Menu
- Add a new "Menu" item in the application header similar to the attached screenshot.
- The menu must support toggle functionality:
- Clicking "Menu" expands the navigation panel.
- Clicking again collapses the navigation panel.
- Use GovUK design system wherever possible.
- Ensure the menu works on desktop and mobile screen sizes.

2. Menu Structure
Create the following menu sections and hyperlinks:

Disease Profiles
- Search disease profiles
- Create profile
- Compare profile versions
- Review Timings

Reports
- General reports
- Questions and guidance reports
- Disease ranking report

Species Data
- View species data
- Maintain species data

3. Razor Pages
- Create a Razor Page for each hyperlink above.
- Use appropriate page names and routes.
- Each page should have:
- Page title matching the menu text.
- Main H1 heading matching the hyperlink text.
- Static placeholder content containing the same hyperlink text and a short description.

Example:
H1: Search disease profiles

Content:
"This page is for Search disease profiles."

4. Breadcrumb Navigation
- Add breadcrumb navigation to every newly created page.
- Format:

Home > Section Name > Page Name

Examples:
Home > Disease Profiles > Search disease profiles
Home > Reports > General reports
Home > Species Data > View species data

- Breadcrumbs should be visible below the header and above the page content.
- Current page should not be a clickable link.

5. Shared Components
- Create reusable partials/components for:
- Header menu
- Breadcrumb navigation
- Avoid duplicating markup across pages.

6. Routing and Navigation
- Ensure all menu links navigate correctly.
- Highlight the active menu item when the corresponding page is open.
- Use Tag Helpers where appropriate.

7. Styling
- Use GovUK Design system wherever possible.
- Keep section headers bold as shown in the screenshot.
- Hyperlinks should be styled consistently with the existing application theme.
- Add minimal CSS only if necessary.

8. Code Quality
- Follow ASP.NET Core Razor Pages best practices.
- Use clean folder structure.
- Add comments only where necessary.
- Ensure solution builds successfully with no warnings or errors.

After implementation, provide:
- List of files created.
- List of files modified.
- Summary of routing changes.
- Summary of menu and breadcrumb implementation.

IMPORTANT:
Before completing the task, create and commit a documentation file at:
docs/HeaderMenuAndNavigationImplementation.md
```

No screenshot image was attached to the request; the header menu was designed from the written
requirements alone (a "Menu" toggle in the service navigation area, expanding a panel with bold
section headings and a list of links per section).

## 2. Analysis of D2R2-2026-08-17 (legacy application)

The legacy VB.NET Web Forms app (`Profiles.Web`) exposes the same broad feature groupings
requested here, via the `.aspx` pages already present in the repository:

| Legacy page | Menu grouping used for apha-cdc-apps |
|---|---|
| `Search.aspx` | Disease Profiles &rarr; Search disease profiles |
| `ManageProfile.aspx` (create flow) | Disease Profiles &rarr; Create profile |
| `CompareProfileVersions.aspx` | Disease Profiles &rarr; Compare profile versions |
| `ReviewEmail.aspx` / review timings report | Disease Profiles &rarr; Review Timings |
| `StaticReports.aspx` | Reports &rarr; General reports |
| `ProfileQuestionHelp.aspx` | Reports &rarr; Questions and guidance reports |
| `ShowDiseaseRankingReport.aspx` | Reports &rarr; Disease ranking report |
| `ViewSpeciesData.aspx` | Species Data &rarr; View species data |
| `MaintainSpeciesData.aspx` | Species Data &rarr; Maintain species data |

The legacy app uses a top navigation bar (`NavigationLinks.ascx`) plus bespoke Defra/Bootstrap CSS.
apha-cdc-apps already uses the GOV.UK Design System (see prior "GDS layout" work), so the new menu
follows the GOV.UK service navigation pattern rather than replicating the legacy markup.

**No changes were made to D2R2-2026-08-17.** All work was implemented only in `apha-cdc-apps`.

## 3. Implementation decisions

- **Razor Pages** were used for the 9 placeholder pages (as required), under `src/CDC.Web/Pages`,
  alongside the existing MVC `LandingController`/views. Both view types share the same
  `Views/Shared/_Layout.cshtml`, so no markup is duplicated between the two.
- **Menu data** is defined once in `Models/HeaderMenu.cs` (a static list of sections/links) so the
  header partial and any future features (e.g. sitemap) can reuse the same source of truth.
- **The "Menu" toggle is a custom component**, not the built-in GOV.UK Frontend
  `govuk-service-navigation` JS behaviour. The official component only shows/hides its own
  `<ul class="govuk-service-navigation__list">` on narrow screens; it does not support an
  always-toggleable, multi-column panel on desktop and mobile as required here. The new toggle
  button and panel reuse GOV.UK classes (`govuk-service-navigation__toggle`, `govuk-width-container`,
  `govuk-grid-row`, `govuk-heading-s`, `govuk-list`, `govuk-link`) for visual consistency, driven by a
  small custom script (`wwwroot/js/header-menu.js`) instead of `govuk-frontend`'s `initAll()`.
  The `data-module="govuk-service-navigation"` attribute was deliberately removed from the section
  so the official component script does not also try to bind to it.
- **Active link highlighting** is computed server-side in the `_HeaderMenu` partial by comparing
  `Url.Page(link.PagePath)` to the current request path, applying `aria-current="page"` and an
  `app-menu-panel__link--active` class — no client-side JS needed for this.
- **Breadcrumbs** are rendered through a `@section Breadcrumbs { ... }` block on each page and
  emitted by the layout between the header and `<main>`. Each page's `PageModel` derives from
  `BreadcrumbPageModelBase`, which builds a `BreadcrumbViewModel(SectionName, PageName)` — this
  avoids repeating breadcrumb markup or wiring in every page.
- The **"Home"** breadcrumb links to the existing `Landing`/`Index` MVC route via Tag Helpers
  (`asp-controller`, `asp-action`). The **section name** (e.g. "Disease Profiles") is not a link,
  because there is no section-overview page in scope for this change; only the **current page**
  is guaranteed non-clickable per the requirement, but showing the section as a link would point to
  a page that doesn't exist, so it is rendered as plain text too.
- **Minimal new CSS** was added to `wwwroot/css/site.css` for the menu toggle/panel only, since GOV.UK
  Frontend has no equivalent "mega menu" component to reuse as-is.

## 4. Files created

Models:
- `src/CDC.Web/Models/HeaderMenu.cs` — menu section/link data (`HeaderMenuSection`, `HeaderMenuLink`, `HeaderMenu.Sections`).
- `src/CDC.Web/Models/BreadcrumbViewModel.cs` — `BreadcrumbViewModel(SectionName, PageName)` record.

Shared Razor Pages infrastructure:
- `src/CDC.Web/Pages/BreadcrumbPageModelBase.cs` — base `PageModel` supplying `Breadcrumb`.
- `src/CDC.Web/Pages/_ViewImports.cshtml` — usings/tag helpers for all Razor Pages.
- `src/CDC.Web/Pages/_ViewStart.cshtml` — points Razor Pages at the shared MVC layout.

Reusable partials (shared components):
- `src/CDC.Web/Views/Shared/_HeaderMenu.cshtml` — renders the menu panel sections/links.
- `src/CDC.Web/Views/Shared/_Breadcrumb.cshtml` — renders the breadcrumb trail.

Client-side:
- `src/CDC.Web/wwwroot/js/header-menu.js` — toggles the menu panel open/closed.

Razor Pages (page + code-behind, 9 pairs):

| Section | Page (route) | Files |
|---|---|---|
| Disease Profiles | `/DiseaseProfiles/Search` | `Pages/DiseaseProfiles/Search.cshtml`, `Search.cshtml.cs` |
| Disease Profiles | `/DiseaseProfiles/Create` | `Pages/DiseaseProfiles/Create.cshtml`, `Create.cshtml.cs` |
| Disease Profiles | `/DiseaseProfiles/CompareVersions` | `Pages/DiseaseProfiles/CompareVersions.cshtml`, `CompareVersions.cshtml.cs` |
| Disease Profiles | `/DiseaseProfiles/ReviewTimings` | `Pages/DiseaseProfiles/ReviewTimings.cshtml`, `ReviewTimings.cshtml.cs` |
| Reports | `/Reports/General` | `Pages/Reports/General.cshtml`, `General.cshtml.cs` |
| Reports | `/Reports/QuestionsGuidance` | `Pages/Reports/QuestionsGuidance.cshtml`, `QuestionsGuidance.cshtml.cs` |
| Reports | `/Reports/DiseaseRanking` | `Pages/Reports/DiseaseRanking.cshtml`, `DiseaseRanking.cshtml.cs` |
| Species Data | `/SpeciesData/View` | `Pages/SpeciesData/View.cshtml`, `View.cshtml.cs` |
| Species Data | `/SpeciesData/Maintain` | `Pages/SpeciesData/Maintain.cshtml`, `Maintain.cshtml.cs` |

Documentation:
- `docs/HeaderMenuAndNavigationImplementation.md` (this file).

## 5. Files modified

- `src/CDC.Web/Views/Shared/_Layout.cshtml` — added the "Menu" toggle button and panel (via the
  `_HeaderMenu` partial) inside the header, added `@await RenderSectionAsync("Breadcrumbs", ...)`
  between the header and `<main>`, and added a `<script>` reference to `header-menu.js`.
- `src/CDC.Web/wwwroot/css/site.css` — added minimal CSS for `.app-menu-toggle` and `.app-menu-panel`.

## 6. Routing summary

- No changes were required in `Program.cs` — Razor Pages support (`AddRazorPages()` /
  `MapRazorPages()`) was already registered for the project.
- Each new page uses the default Razor Pages convention-based route (folder + file name), e.g.
  `Pages/DiseaseProfiles/Search.cshtml` &rarr; `/DiseaseProfiles/Search`.
- The existing MVC default route (`{controller=Landing}/{action=Index}/{id?}`) is unchanged.

## 7. Menu implementation summary

- The header's service navigation section (`_Layout.cshtml`) contains a `Menu` `<button>` with
  `aria-expanded` / `aria-controls`, followed by a hidden panel (`#app-menu-panel`).
- The panel markup itself lives in `_HeaderMenu.cshtml`, iterating `HeaderMenu.Sections` and
  rendering one `govuk-grid-column-one-third` per section with a bold `govuk-heading-s` title and a
  `govuk-list` of links.
- `header-menu.js` toggles `hidden` on the panel and flips `aria-expanded` on the button — the same
  behaviour applies at both desktop and mobile widths (no media-query-gated logic), satisfying the
  "works on desktop and mobile" requirement.
- The currently active link is detected server-side (`Url.Page(...)` vs. request path) and marked
  with `aria-current="page"` and a bold style.

## 8. Breadcrumb implementation summary

- Format: `Home > Section Name > Page Name`, rendered as a GOV.UK `govuk-breadcrumbs` list.
- `Home` is a Tag Helper link (`asp-controller="Landing" asp-action="Index"`).
- `Section Name` and `Page Name` are plain text (not links) — the current page must never be a
  link per the requirement, and no section-overview page exists yet to link the section name to.
- Each page model derives from `BreadcrumbPageModelBase`, passing its section/page name once in its
  constructor; the `.cshtml` then renders `<partial name="_Breadcrumb" model="Model.Breadcrumb" />`
  inside a `@section Breadcrumbs { ... }` block, which the layout renders directly below the header.

## 9. Build verification

`dotnet build src/CDC.Web/CDC.Web.csproj` completed with **0 warnings, 0 errors** after these changes.
