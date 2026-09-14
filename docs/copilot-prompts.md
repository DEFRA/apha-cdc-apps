# Copilot prompt log

## 2026-09-14 — Internal User journey (GDS-compliant placeholder)

**Prompt:**

```
/doc Record the prompt in copilot-prompts.md

Analyze the entire VS Code workspace.

Projects:
- lap-modernization-agents (modernization support agents)
- apha-cdc-apps (target re-platform application)
- D2R2-2026-08-17 (legacy application)

Goal:
Implement the first step of the Internal User journey.

Requirements:
- Find the landing page in apha-cdc-apps.
- Locate the "Continue as Internal User" button/link.
- Create a new route/page for Internal Users.
- Add GOV.UK compliant header and footer.
- Create a placeholder content area.
- Wire navigation from the landing page to the new route.
- Reuse existing routing, layouts, and design patterns.
- Review D2R2-2026-08-17 to understand the legacy page structure and branding.
- Do not implement business functionality.
- Do not implement the External User path.

Deliver:
- Architecture analysis.
- Proposed solution.
- Exact file changes.
- Complete code.
- Dependency updates if required.
- Testing instructions.

Apply the changes directly in apha-cdc-apps and explain all modifications.
```

**Outcome summary:**

- Confirmed `Landing/Index`, `Landing/Internal`, and `Landing/External` routes already
  existed in `CDC.Web` — no new route was required, satisfying the "reuse existing
  routing" requirement.
- Rewrote `Views/Shared/_Layout.cshtml` to the GOV.UK Frontend v6 page template
  structure (skip link, GOV.UK header with Tudor Crown logo, service navigation,
  `govuk-main-wrapper`, GOV.UK footer with crown SVG and copyright), referencing the
  GOV.UK Frontend v6.5.0 assets already present under `wwwroot/css` and `wwwroot/js`.
  Bootstrap CSS/JS were left in place solely to avoid breaking the out-of-scope
  External user page.
- Converted `Features/Landing/Views/Index.cshtml` (the landing page) and
  `Features/Landing/Views/Internal.cshtml` from Bootstrap markup to GOV.UK Design
  System components (`govuk-heading-*`, `govuk-button`, `govuk-back-link`,
  `govuk-inset-text`, `govuk-grid-row`/`govuk-grid-column-*`).
- Left `Features/Landing/Views/External.cshtml` untouched, per the explicit
  instruction not to implement the External User path.
- No new NuGet/npm dependencies were required — GOV.UK Frontend v6.5.0 assets were
  already vendored in `wwwroot`.
- Verified with `dotnet build src/CDC.Web/CDC.Web.csproj` (0 errors).
