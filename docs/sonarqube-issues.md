# SonarQube Issues Report
PR: #14 - Feature/view-species-data

## Summary

Total Issues: 56

### By Severity
- Critical: 20+
- Major: Multiple
- Minor: Multiple

### High Priority Files

1. src/CDC.Web/wwwroot/js/tree-view.js
   - 25+ issues
   - Main contributor to Quality Gate failure

2. govuk-tree-picker/index.html
   - Accessibility issues
   - JavaScript quality issues
   - Complexity issues

3. src/CDC.Api/Features/Species/Commands/UpdateSpeciesAnswerDataCommand.cs
   - ASP.NET model binding issue

4. src/CDC.Web/Pages/ViewSpeciesData.cshtml
   - Accessibility issue

5. src/CDC.Web/Pages/ViewSpeciesData.cshtml.cs
   - Duplicate string literals

6. src/CDC.Web/Views/Shared/_TreeViewNode.cshtml
   - HTML structure issue

---

# File: UpdateSpeciesAnswerDataCommand.cs

Path:
src/CDC.Api/Features/Species/Commands/UpdateSpeciesAnswerDataCommand.cs

Line:
64

Rule:
S6964

Severity:
Major

Issue:
Value type property used as input in a controller action should be nullable, required or annotated with JsonRequiredAttribute.

Required Fix:
- Review all input model properties.
- Convert value types used in API requests to:
  - required
  - nullable
  - or JsonRequired

Example:

Before:

public int SpeciesId { get; set; }

After:

public required int SpeciesId { get; set; }

---

# File: ViewSpeciesData.cshtml

Path:
src/CDC.Web/Pages/ViewSpeciesData.cshtml

Line:
41

Rule:
S6819

Severity:
Major

Issue:
Use <output> instead of role="status".

Required Fix:

Before:

<div role="status">...</div>

After:

<output>...</output>

---

# File: ViewSpeciesData.cshtml.cs

Path:
src/CDC.Web/Pages/ViewSpeciesData.cshtml.cs

Line:
15

Rule:
S1192

Severity:
Minor

Issue:
Literal 'species' repeated four times.

Required Fix:

Create constant:

private const string SpeciesKey = "species";

Replace all occurrences.

---

# File: _TreeViewNode.cshtml

Path:
src/CDC.Web/Views/Shared/_TreeViewNode.cshtml

Line:
7

Rule:
ItemTagNotWithinContainerTagCheck

Severity:
Minor

Issue:
<li> must be inside <ul>, <ol> or <menu>.

Required Fix:

Before:

<li>...</li>

After:

<ul>
    <li>...</li>
</ul>

---

# File: govuk-tree-picker/index.html

## Security

### Line 11

Rule:
S5725

Severity:
Minor

Issue:
External script or stylesheet missing integrity attribute.

Fix:

...

---

## Accessibility

### Line 225

Rule:
S6819

Issue:
Use <output> instead of role=status.

Fix:

<output id="selectedCount"></output>

---

## JavaScript Style Issues

### Lines
507
607
639
673
679
694
699
706
714
717
720
721
722
729
745

Rule:
S121

Severity:
Critical

Issue:
Missing braces around if/else blocks.

Fix:

Before:

if (condition)
    doSomething();

After:

if (condition)
{
    doSomething();
}

---

## Line 589

Rule:
S1192

Severity:
Critical

Issue:
Repeated string literal.

Fix:

Extract constant.

Before:

if (type === "selected")

After:

const STATUS_SELECTED = "selected";

if (type === STATUS_SELECTED)

---

## Line 683

Rule:
S3358

Severity:
Major

Issue:
Nested ternary expression.

Fix:

Replace ternary with if/else statement.

---

## Lines 704-745

Rule:
S3776
Rule:
S1541

Severity:
Critical

Issue:
Function complexity exceeds limit.

Current:
- Cognitive Complexity = 20
- Allowed = 15

Current:
- Cyclomatic Complexity = 18
- Allowed = 10

Required Fix:
- Split function into smaller helper functions.
- Extract decision logic.
- Reduce nesting.

---

# File: tree-view.js

Path:
src/CDC.Web/wwwroot/js/tree-view.js

## Naming

Line:
14

Rule:
S100

Issue:
TreeView function naming pattern violation.

Required Fix:
Review naming convention or convert to class.

---

## Major Refactor Required

Rules:
S3525

Lines:
47
51
55
59
63
70
76
81
87
101
144
157
162
182
207
217
229
243
247

Issue:
Methods attached to constructor function should be moved into ES6 class.

Required Fix:

Convert:

function TreeView() {
}

TreeView.prototype.method = function() {
}

To:

class TreeView {
    method() {
    }
}

---

## String Concatenation

Lines:
117
237
238

Rule:
S3512

Issue:
Unexpected string concatenation.

Fix:

Before:

"Count: " + value

After:

`Count: ${value}`

---

## Duplicate Literal

Line:
125

Rule:
S1192

Severity:
Critical

Issue:
Repeated string literal.

Fix:
Extract constant.

---

## Nested Ternary

Line:
238

Rule:
S3358

Severity:
Major

Issue:
Nested ternary operation.

Fix:
Replace with if/else.

---

# Recommended Fix Order

1. Fix all 