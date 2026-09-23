---
name: Enterprise component playbook
description: Use when discovering, documenting, or updating reusable UI components so developers can find and reuse them. Framework-agnostic (Angular first; React, Vue, Next.js later). Analyzes source and writes developer-facing docs — does not build a playbook UI, Storybook app, or design-system site.
---
# Enterprise component playbook

## Goal
Keep a **living catalog** of reusable UI components: what they are for, how to use them, and what not to do. Docs match the implementation. New developers should find a component here **before** copying a one-off.

This skill **analyzes components and writes/updates markdown**. It does **not** build a playbook website, Storybook, Bit, Chromatic, or an in-app gallery. If the repo already has Storybook (OSS), you may add a link — do not introduce a commercial catalog product.

## When to use
- A shared/reusable component is added, renamed, or its public API changes
- Onboarding or “what components do we have?”
- Code review finds a new public UI primitive without a playbook page
- A second UI framework appears (React/Vue/Next) and needs the **same** page shape

Skip one-off feature pages, routed screens, generated code, and third-party package internals.

## Ownership

| Piece | Owner |
|-------|--------|
| Extracting API from source and writing/updating the page | The **UI specialist who owns that component** (Angular today; Ionic for `ion-*` wrappers; a future React/Vue specialist for that tree) |
| Catalog index linked from app docs | Documentation specialist (does not invent props) |
| Flagging missing/stale pages on a PR | Code review |
| Whether something is a shared primitive vs feature-local | Architecture only if disputed; default = folder + public export (below) |

Do **not** spawn a Component Playbook agent. Master / Angular / Documentation load this skill.

## Framework-agnostic contract

Every page uses the **same sections** (below). Framework-specific names map as:

| Playbook field | Angular | React / Next | Vue |
|----------------|---------|--------------|-----|
| Export name | class / `selector` | function or `displayName` | SFC name / `name` |
| Inputs | `@Input` / `input()` | props | `defineProps` / `props` |
| Outputs | `@Output` / `output()` | `on*` callbacks | `defineEmits` / `emits` |
| Content | `ng-content` / templates | `children` / render props | slots |
| Variants | inputs, host classes, directives | variant props | props / variants |

Detect the framework from the file (`.component.ts`, `.tsx`, `.vue`) and the nearest package manifest. **Do not rewrite a React page into Angular examples or vice versa.** One page per public component; `framework` in the frontmatter.

Angular is the default in this pack. React, Vue, and Next.js pages are valid when those trees exist — same template, different usage snippets.

## What counts as reusable

Include a component when **two or more** of these are true:

- Lives under a shared/ui/design-system/components library (not a feature route folder)
- Is in a public barrel (`index.ts`, `public-api.ts`) or explicitly exported
- Has a stable selector/export intended for other features
- Encapsulates a control pattern (button, field, table, dialog, empty-state, permission-aware action)

Exclude: app shells, routed pages, test doubles, Storybook extra wrappers (if any), `node_modules`, CLI-generated placeholders with no API.

## Doc location (do not invent a second tree)

Prefer, in order:

1. Repo convention if one exists (`docs/components/`, `ui/README`, etc.)
2. Else `docs/components/<framework>/<ComponentName>.md` plus `docs/components/README.md` (catalog index)
3. A short `COMPONENT.md` beside the source **only** if the repo already documents that way — then still list it in the index

Never put secrets, tokens, or environment values in playbook pages. Open-source / built-in components only; do not document a commercial UI kit as “ours” unless the user already accepted it (then document the **wrapper**, not the vendor catalog).

## Page template (required)

Use this structure. Omit a subsection only if it truly does not apply; write `None` rather than deleting the heading.

```markdown
---
name: <Export or common name>
framework: angular | react | vue | next | ionic-angular
status: stable | experimental | deprecated
source: <path to implementation>
export: <selector, function, or SFC name>
---

# <Name>

## Purpose
One short paragraph: the problem it solves.

## When to use
- …

## When not to use
- … (point at the component they should use instead, if any)

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|

### Outputs / events
| Name | Payload | Description |
|------|---------|-------------|

### Content / slots
How callers project children or templates.

### Configuration
Standalone vs module, required providers, CSS custom properties, i18n inputs.

## Variants and states
Default, disabled, loading, error, empty, size/tone variants — only those the **code** supports. Show how each is triggered (prop/class).

## Usage
Copy-paste snippets in the component’s framework. At least: minimal use, one variant, one realistic enterprise case (form field, table row action, permission-gated button). Use permission helpers — never role names.

## Accessibility
Labeling, keyboard, focus, ARIA that **this** component implements or requires of the caller. Note gaps; do not invent WCAG scores.

## Dependencies
Peer packages, design tokens, other playbook components. No commercial libraries as a default.

## Do
- …

## Don’t
- …

## Common mistakes
- Symptom → why it happens → correct usage (link to API)

## Related
Links to other playbook pages.

## Source of truth
Implementation path + symbols read (e.g. `ButtonComponent` inputs). If code and this page disagree, **code wins** — fix this page.
```

## Steps

1. **Detect** UI framework(s) and existing `docs/components` (or equivalent). Reuse the tree; do not fork a second catalog.
2. **Discover** candidates from shared folders, barrels, and selectors/exports. Produce a list: new / changed / removed / skip (with reason).
3. **Extract** the public API from source — types, defaults, required flags, outputs, slots. Do not document private fields, internal services, or commented-out props. Do not guess undocumented behavior.
4. **Create or update** the page with the template. For updates: diff the previous API table against source; remove stale props; add new ones; refresh snippets.
5. **Variants/states** — only what templates/class bindings/tests actually exercise or inputs clearly name (`disabled`, `loading`, `appearance`, etc.).
6. **Examples** — compile in the same style as the repo (standalone Angular, JSX, Vue SFC). Keep them short. Dummy labels are fine; no real secrets.
7. **A11y** — read the template/JSX for `aria-*`, labels, focus traps. If the component is a public control and has no name/label story, say so as a gap (Angular client-quality / future React a11y skill still apply).
8. **Index** — `docs/components/README.md` (or repo equivalent): table of name, framework, status, purpose one-liner, link. Remove rows for deleted components; mark `deprecated` if the export remains but should not be used.
9. **Handoff** using `skills/enterprise-team-collaboration.md`.

When a component is renamed or moved, update `source`, `export`, the filename if needed, and the index. Do not leave a second page.

## Quality bar

- Every public input/output on the component appears in the table (and nothing extra)
- Snippets match current selectors/prop names
- Permission-gated examples use permission codes, not role names
- No playbook UI, no paid design-system SaaS
- Feature-local widgets stay out of the catalog
- Pages for different frameworks share headings, not copy-pasted framework APIs

## Report back

- Framework(s) detected
- Components discovered: added / updated / skipped / removed
- Catalog index path
- API gaps (public export with no page, page with no source)
- A11y gaps left for the UI specialist
