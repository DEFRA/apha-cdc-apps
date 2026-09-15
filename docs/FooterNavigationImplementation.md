# Footer Navigation Implementation

## Objective

Update the `apha-cdc-apps` application footer to match the legacy D2R2 footer (three
link sections: User Admin, Cross Profile Admin, Help and Support, plus a "Back to
top" link), create static placeholder Razor Pages for each footer link, wire the
navigation, and add breadcrumb trails to every new page.

## Files created

Razor Pages (page + code-behind, 7 pairs):

| Section | Page (route) | Files |
|---|---|---|
| User Admin | `/UserAdmin/GlobalUsers` | `Pages/UserAdmin/GlobalUsers.cshtml`, `GlobalUsers.cshtml.cs` |
| User Admin | `/UserAdmin/ExternalUsers` | `Pages/UserAdmin/ExternalUsers.cshtml`, `ExternalUsers.cshtml.cs` |
| Cross Profile Admin | `/CrossProfileAdmin/ReferenceData` | `Pages/CrossProfileAdmin/ReferenceData.cshtml`, `ReferenceData.cshtml.cs` |
| Cross Profile Admin | `/CrossProfileAdmin/PrioritisationVariables` | `Pages/CrossProfileAdmin/PrioritisationVariables.cshtml`, `PrioritisationVariables.cshtml.cs` |
| Cross Profile Admin | `/CrossProfileAdmin/CrossCuttingIssueScores` | `Pages/CrossProfileAdmin/CrossCuttingIssueScores.cshtml`, `CrossCuttingIssueScores.cshtml.cs` |
| Help and Support | `/HelpSupport/HelpUsingD2R2` | `Pages/HelpSupport/HelpUsingD2R2.cshtml`, `HelpUsingD2R2.cshtml.cs` |
| Help and Support | `/HelpSupport/QualityStatement` | `Pages/HelpSupport/QualityStatement.cshtml`, `QualityStatement.cshtml.cs` |

Documentation:
- `docs/FooterNavigationImplementation.md` (this file).

All 7 new `PageModel` classes derive from the existing `BreadcrumbPageModelBase`
(added in a previous change), so no new breadcrumb infrastructure was required.

## Files modified

- `src/CDC.Web/Views/Shared/_Layout.cshtml` — the `<footer>` was fully replaced.
  The previous GOV.UK crown/copyright/licence footer content was removed entirely
  and replaced with:
  - A "Back to top" link (with the standard GOV.UK-style up-arrow icon) above the
    footer sections, linking to `#main-content`.
  - Three `app-footer__section` columns — **User Admin**, **Cross Profile Admin**,
    **Help and Support** — each with a `govuk-heading-m` heading and a
    `govuk-list` of `asp-page` links, mirroring the legacy `Footer.ascx` structure
    from `D2R2-2026-08-17/Profiles.Web/Footer.ascx`.
- `src/CDC.Web/wwwroot/css/site.css` — added `.app-footer*` rules (flexbox section
  layout, back-to-top alignment, responsive stacking at the tablet breakpoint).
  No other existing rules were changed.

No files were changed outside `apha-cdc-apps`.

## Navigation links added

All footer links use the `asp-page` Tag Helper (not raw `href`s), so routes are
resolved and validated by the Razor Pages routing system:

| Link text | Target page |
|---|---|
| Global users | `/UserAdmin/GlobalUsers` |
| External users | `/UserAdmin/ExternalUsers` |
| Maintain reference data | `/CrossProfileAdmin/ReferenceData` |
| Maintain prioritisation variables | `/CrossProfileAdmin/PrioritisationVariables` |
| Maintain cross-cutting issue scores | `/CrossProfileAdmin/CrossCuttingIssueScores` |
| Help using D2R2 | `/HelpSupport/HelpUsingD2R2` |
| D2R2 Quality Statement | `/HelpSupport/QualityStatement` |

No changes were made to `Program.cs` — Razor Pages support was already registered
for the project, and pages route by convention (folder + file name).

## Breadcrumb implementation approach

Each new page reuses the existing breadcrumb infrastructure introduced for the
header-menu pages:

- `BreadcrumbPageModelBase` (constructor takes `sectionName`, `pageName`) builds a
  `BreadcrumbViewModel` exposed as `Model.Breadcrumb`.
- Each `.cshtml` renders `<partial name="_Breadcrumb" model="Model.Breadcrumb" />`
  inside a `@section Breadcrumbs { ... }` block.
- The shared layout renders `@await RenderSectionAsync("Breadcrumbs", ...)`
  directly below the header and above `<main>`, so breadcrumbs appear near the top
  of every page.
- `_Breadcrumb.cshtml` renders `Home` as a Tag Helper link
  (`asp-controller="Landing" asp-action="Index"`); the section name and current
  page name are rendered as plain text (not links) — the current page is never
  clickable, matching the "current page not clickable" requirement. The section
  name is also not a link because no section-overview page exists in scope for
  this change (same approach as the earlier Disease Profiles / Reports / Species
  Data pages).

Example rendered trail: `Home > User Admin > Global users`.

## Testing performed

- `dotnet build src/CDC.Web/CDC.Web.csproj` — completed with **0 warnings, 0
  errors**.
- Manually reviewed the generated footer markup and each new page's Razor syntax
  for correct `asp-page` route targets against the 7 routes above.

## Summary of results

- Footer fully replaced with the three-section layout (User Admin, Cross Profile
  Admin, Help and Support) plus a "Back to top" link, using existing GOV.UK
  Frontend classes (`govuk-heading-m`, `govuk-list`, `govuk-link`) so no new
  design-system-equivalent styling was needed beyond simple flex layout rules.
- 7 new static Razor Pages created, each with a page title, `<h1>` matching the
  link text, a short placeholder description, and a breadcrumb trail.
- All changes were confined to `apha-cdc-apps`; no other project in the
  workspace was modified.

---

## A. Original implementation prompt

```
Reference Project: D2R2-2026-08-17
Target Project: alpha-cdc-apps ONLY

Objective:
Update the application footer to match the attached screenshot and create supporting Razor Pages with static content.

Requirements:

1. Footer Changes
- Remove all existing footer content.
- Replace the footer with three sections exactly as shown in the screenshot:

Section 1: User Admin
- Global users
- External users

Section 2: Cross Profile Admin
- Maintain reference data
- Maintain prioritisation variables
- Maintain cross-cutting issue scores

Section 3: Help and Support
- Help using D2R2
- D2R2 Quality Statement

- Include a "Back to top" link above the footer sections.
- Use existing application styles where possible.
- Ensure responsive layout across desktop and tablet resolutions.

2. Razor Pages Creation
Create individual Razor Pages (.cshtml and PageModel as required) with static placeholder content for the following pages:

User Admin
- /UserAdmin/GlobalUsers
- /UserAdmin/ExternalUsers

Cross Profile Admin
- /CrossProfileAdmin/ReferenceData
- /CrossProfileAdmin/PrioritisationVariables
- /CrossProfileAdmin/CrossCuttingIssueScores

Help and Support
- /HelpSupport/HelpUsingD2R2
- /HelpSupport/QualityStatement

3. Navigation
- Create navigation links from the footer to each corresponding Razor Page.
- Use tag helpers where appropriate.
- Ensure links are routed correctly.

4. Breadcrumb Trail
Add breadcrumb navigation to all newly created pages.

Example:

Home > User Admin > Global Users

Home > Cross Profile Admin > Maintain Reference Data

Home > Help and Support > Help Using D2R2

Requirements:
- Breadcrumb must be visible near the top of each page.
- Current page should be displayed but not clickable.
- Parent pages should be clickable links.

5. Static Content
For each page:
- Add a meaningful page title.
- Add an H1 heading.
- Add placeholder descriptive text explaining the purpose of the page.
- Keep content static only.
- No backend logic, database integration, API calls, or data access.

6. Scope Restrictions
IMPORTANT:
- Make changes ONLY within the alpha-cdc-apps project.
- Do not modify any other project in the solution.
- Do not introduce breaking changes to existing navigation.
- Reuse existing shared layout and styling components where available.

7. Documentation
Create a new file:

docs/FooterNavigationImplementation.md

Document:
- Objective
- Files created
- Files modified
- Razor Pages added
- Navigation links added
- Breadcrumb implementation approach
- Testing performed
- Summary of results

At the end of the document include:
A. Original implementation prompt.
B. Detailed implementation summary.
C. Any assumptions made during development.

Deliverables:
1. Updated footer matching the screenshot.
2. New Razor Pages with static content.
3. Footer navigation links wired to pages.
4. Breadcrumb navigation implemented.
5. Documentation file created in docs/FooterNavigationImplementation.md.
6. All changes confined to alpha-cdc-apps project only.
```

## B. Detailed implementation summary

See "Files created", "Files modified", "Navigation links added", "Breadcrumb
implementation approach" and "Summary of results" sections above — they together
constitute the full implementation summary.

## C. Assumptions made during development

- **No screenshot was actually attached** to the request (or to the prior header
  requests in this session). The footer layout was implemented by directly
  reusing the legacy `D2R2-2026-08-17/Profiles.Web/Footer.ascx` structure (three
  columns with the exact same headings and link text), which is the closest
  available source of truth referenced in the prompt.
- The project name in the prompt, "alpha-cdc-apps", was treated as referring to
  the actual workspace project **`apha-cdc-apps`** (the only C# project present in
  the workspace matching that description).
- Breadcrumb "page name" casing follows the exact footer link text (e.g.
  "Global users", lower-case "u") rather than the slightly different capitalised
  wording used in the prompt's breadcrumb examples ("Global Users"), to keep the
  footer link, page `<h1>`, page title, and breadcrumb trail consistent with each
  other.
- The GOV.UK crown copyright / Open Government Licence footer content that was
  previously present was removed in full, per "Remove all existing footer
  content" — the legacy D2R2 footer being replicated does not include this
  content either.
- Placeholder page body text follows the same "This page is for &lt;X&gt;." style
  established by the earlier Disease Profiles / Reports / Species Data pages, with
  one additional sentence describing the page's intended future purpose, to
  satisfy "add placeholder descriptive text explaining the purpose of the page."
