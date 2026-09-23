---
name: Enterprise Angular client quality
description: Use when implementing Angular state, API integration, performance, and accessibility for an enterprise SPA or PWA. Scaffold the app first with the Angular scaffold skill. Not for Nx or Ionic shell.
---
# Enterprise Angular client quality

## Goal
Build Angular UI that is typed against the API, fast enough for enterprise lists, and usable with keyboard/AT — without extra state frameworks or commercial UI kits.

## When to use
After `skills/enterprise-angular-app-scaffold.md` (or when extending features). Use `skills/enterprise-pwa-offline.md` in addition when installable/offline. Ionic reuses these patterns inside `ion-*` pages.

## Ownership

- **Angular** owns this skill.
- **.NET** owns OpenAPI/DTOs; Angular does not invent a second contract.
- **Auth** owns interceptor credential mode (Bearer vs cookies vs OIDC).
- **Testing** owns e2e/a11y-at-scale; Angular writes component/unit tests for logic it adds.

## State management

Default: **Angular signals + services** (and `resource` / `httpResource` when the repo’s Angular version has them). Feature state lives with the feature.

Use a store library only when many disconnected features share complex orchestration **and** the repo already standardized on it. NgRx is OSS — still not the default. Do not add commercial state tools. Do not add NgRx, Akita, or Elf “just in case.”

Rules:

- Server data is the source of truth; client state is view/selection/form draft/outbox
- No `any` on HTTP responses; generate or hand-type DTO interfaces from OpenAPI
- Permission flags come from the permission service, not copied into every store

## API integration

- One `HttpClient` interceptor stack: correlation id, credentials (after Auth choice), problem-details → user message
- Typed services per API area (mirroring modules), not a god `ApiService`
- Consume OpenAPI (generated client **or** thin typed wrappers). Regeneration must not be a paid product (use `ng-openapi-gen`, NSwag, or Microsoft `kiota` — all OSS)
- Timeouts and retries: retry only idempotent GETs; mutations follow PWA outbox if offline
- Loading / empty / error UI is mandatory on list and save paths
- Never store tokens in `localStorage` if Auth chose a safer option (see JWT skill)

## Performance

- Lazy-load feature routes; keep the shell small
- Bundle budgets in `angular.json`; fail CI on budget overruns when DevOps wires it
- Large tables: CDK virtual scroll (OSS) or paginate via the API — do not render 10k rows
- Track lists with stable ids
- Avoid unnecessary re-renders: signals as default; `ChangeDetectionStrategy.OnPush` on heavy components if not already signal-driven
- Images/icons: compressed, sized; no huge commercial icon kits
- Measure with Lighthouse/CLI or Angular DevTools — do not invent scores

## Accessibility

- Every input has a name/label; errors are tied with `aria-describedby`
- Focus moves to the error summary or first invalid field on submit
- Keyboard: all primary actions reachable; do not trap focus except in dialogs
- Color is not the only status signal
- Hit targets usable on pointer and touch (PWA/mobile browser)
- Prefer Angular Material **or** native controls — both OSS. No Kendo/Telerik/Syncfusion/Clarity paid kits
- Route titles and one `h1` per view

## Steps

1. Confirm DTO/OpenAPI and permission codes for the feature.
2. Add the feature with `ng generate`; put HTTP in a service, UI in standalone components.
3. Wire permission directive/guard; hide actions the API would 403.
4. Apply loading/empty/error; virtualize or paginate lists.
5. Keyboard-check the primary path; add unit tests for mapping/state logic.
6. If PWA: outbox and offline banner per PWA skill — do not duplicate stores.
7. If the feature introduced or changed a **shared** component, follow `skills/enterprise-component-playbook.md` and update that component's catalog page (reuse existing primitives first).

## Quality bar

- No role-name checks
- No commercial UI/state/analytics libraries
- No secrets in environment files committed to git
- Primary flow works with keyboard
- HTTP errors surface a correlation id in dev builds

## Report back

- State approach (signals vs existing store)
- How DTOs are typed/generated
- Perf notes (lazy routes, virtual scroll, budgets)
- A11y gaps still open
- Auth/PWA wiring status
- Playbook pages added/updated (or N/A)
