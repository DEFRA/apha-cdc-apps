# Internal.cshtml — D2R2 Home Page Redesign Prompt

## Original prompt

```
a {
    text-decoration: none;
    color: #464feb;
}
tr th, tr td {
    border: 1px solid #e6e6e6;
}
tr th {
    background-color: #f5f5f5;
}
**Prompt: Modify `Internal.cshtml` to Match D2R2 Home Page**

Clear all existing content from `Internal.cshtml` and replace it with a new static landing page based on the content and layout from `Home.aspx` in **D2R2-2026-08-17**.

### Requirements

#### Design & Framework

- Use the **GOV.UK Design System** components, spacing, typography, colours, and grid layout.
- The final UI should visually resemble the attached screenshot as closely as possible while remaining GOV.UK compliant.
- Create a responsive layout using the GOV.UK grid system.

#### Page Structure

##### Header Section

- Display a large page heading: **D2R2**
- Below the heading, add the following static description text:

> D2R2 is a comprehensive, peer-reviewed animal disease database and risk ranking tool. It is focused on the UK situation. Its standardised approach facilitates a transparent mechanism for making policy decisions.

##### Main Content Layout

Use a two-column layout.

**Left Column (approximately 70%)**
Create a section titled:

**Popular on D2R2**

Add the following static cards/rows with GOV.UK styling:

1. **Search Disease Profiles**

- Find information about diseases and the species they affect.
- Dummy hyperlink (`#`)
2. **Disease Ranking Report**

- View Disease ranking report.
- Dummy hyperlink (`#`)
3. **General Reports**

- View a list of reports.
- Dummy hyperlink (`#`)
4. **View Species Data**

- Find information about specific species.
- Dummy hyperlink (`#`)

Each item should:

- Be separated by horizontal dividers.
- Be styled as a clickable row.
- Include a right-facing chevron/icon aligned to the right.

#### Right Column (approximately 30%)

##### D2R2 Image

- Reuse the existing D2R2 image/logo from the application.
- Maintain the **same size and proportions** as shown in the screenshot.
- Position it above the Quick Links section.

##### Quick Links Section

Title:

**Quick Links**

Add dummy hyperlinks:

- Help Using D2R2
- Compare Profile Versions
- Create Disease Ranking Filter

All links should point to `#`.

#### Footer Section

Add a footer area similar to the screenshot containing three columns:

##### User Admin

- Global users
- External users
- Internal users

##### Cross Profile Admin

- Maintain reference data
- Maintain prioritisation variables
- Profile administration

##### Help and Support

- Help using D2R2
- D2R2 Quality Statement
- Contact Support

Use static text and dummy hyperlinks only.

### Technical Requirements

- Remove all existing page-specific functionality, controls, and content from `Internal.cshtml`.
- Use Razor syntax only where required for layout integration.
- Do not implement any backend functionality.
- Use static content and placeholder (`#`) hyperlinks.
- Ensure accessibility compliance consistent with GOV.UK Design System guidance.
- Follow semantic HTML structure (`main`, `section`, `nav`, `footer`, headings hierarchy, etc.).
- Preserve application master layout and shared navigation.
- Match the spacing, alignment, typography hierarchy, and overall visual appearance of the attached screenshot as closely as possible.

### Expected Outcome

A GOV.UK-styled D2R2 landing page that visually matches the provided screenshot, uses static content and dummy links, retains the existing D2R2 image at the same size, and completely replaces the current contents of `Internal.cshtml`.

Save the prompt in a new file under docs.
```

## Notes on implementation

- No screenshot was actually attached to the request; the page was built directly
  from the written requirements and the equivalent legacy markup in
  `D2R2-2026-08-17/Profiles.Web/Home.aspx`.
- The D2R2 logo (`Profiles.Web/Images/D2R2LogoSmall.png`) was copied from
  `D2R2-2026-08-17` into `apha-cdc-apps/src/CDC.Web/wwwroot/assets/images/D2R2LogoSmall.png`
  so it could be reused, per "Reuse the existing D2R2 image/logo from the
  application" — no such image previously existed in `apha-cdc-apps`.
- The "Footer Section" in the prompt was **not** duplicated inside
  `Internal.cshtml`. The shared layout (`Views/Shared/_Layout.cshtml`) already
  renders a three-column footer (User Admin / Cross Profile Admin / Help and
  Support) below every page, and the prompt's own technical requirements say to
  "preserve application master layout and shared navigation". Adding a second,
  slightly different `<footer>` inside the page content would duplicate the
  landmark and conflict with the shared one. The minor link differences in the
  prompt (e.g. "Internal users", "Profile administration", "Contact Support")
  were not applied to the shared footer, since this change was scoped to
  `Internal.cshtml` only.
- The row-list styling for "Popular on D2R2" (divider lines, right-aligned
  chevron, clickable row) has no direct GOV.UK Frontend component, so a small
  set of bespoke `.app-home-links*` CSS rules were added to `wwwroot/css/site.css`,
  reusing GOV.UK colour/spacing tokens (`--govuk-link-colour`,
  `--govuk-border-colour`, `--govuk-focus-colour`) rather than introducing new
  colours.
- Build verified with `dotnet build src/CDC.Web/CDC.Web.csproj` — 0 warnings, 0 errors.
