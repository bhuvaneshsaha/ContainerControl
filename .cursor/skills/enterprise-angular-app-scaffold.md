---
name: Enterprise Angular app scaffold
description: Use when scaffolding or extending an Angular enterprise SPA with the Angular CLI (workspace, routing, environments, permission helpers). For state, HTTP quality, performance, and a11y follow the Angular client-quality skill. For PWA/offline follow the PWA skill. Not Nx, not Ionic shell.
---
# Enterprise Angular app scaffold

## Goal
Produce a production-ready Angular CLI feature or app shell (not Nx).

## Defaults
- Standalone components unless the repo standardizes on NgModules
- Strict TypeScript; path aliases; lazy-loaded feature routes
- State, HTTP, performance, accessibility: `skills/enterprise-angular-client-quality.md` (signals + services by default; no extra store library)
- PWA / service worker / IndexedDB / sync: only if Architecture asked — `skills/enterprise-pwa-offline.md`. Do not add Ionic to obtain a PWA
- HTTP via interceptors: auth token or cookie credentials (after Auth specialist choice), correlation id, error mapping (`skills/enterprise-observability.md`)
- Environments for dev/staging/prod; never commit secrets
- Reactive forms for complex input
- ESLint + Prettier conventions matching the repo
- Open-source or Angular built-ins only — no commercial UI kits or plugins
- Dummy/sample data allowed for local development
- Authorization: permission service, route guards, and `*hasPermission` — never role-name checks. Role admin UI composes roles from the permission catalog (`skills/enterprise-permission-based-access.md`)
- Shared/reusable UI: after `ng generate` of a public component, document it with `skills/enterprise-component-playbook.md` (markdown catalog — not a gallery app)

## CLI first (do not hand-write a new app)
Angular CLI produces `angular.json`, `tsconfig`, and the current workspace layout. Hand-scaffolding that from scratch wastes time and drifts from the real template.

1. **Check:** `ng version` (or `npx -y @angular/cli@latest version`). Need Node.js LTS + npm/pnpm.
2. **If present:** `ng new` for a new app (`--routing`, style matching the repo, standalone as default, **not Nx**). Then `ng generate` for components, services, guards, interceptors, pipes.
3. After CLI output exists, add feature folders, permission helpers, and interceptors — **do not recreate** `package.json` / `angular.json` by hand.
4. **If CLI/Node is missing:** stop. Tell the user to install Node.js LTS and `@angular/cli` (`npm i -g @angular/cli`). Ask whether to wait. Hand-write the app shell **only if the user explicitly agrees to a no-CLI fallback**; say so in the report.

Extending an existing Angular app: always `ng generate` (or the repo's equivalent) instead of creating files from a blank template.

## Steps
1. Confirm UX flows, API contracts, and that Auth has the user's chosen approach (Cookie / JWT / OIDC) before adding guards or interceptors.
2. Check Angular CLI; create the app/feature with `ng new` / `ng generate` (Angular CLI, not Nx).
3. Add routing, interceptors, and **permission** guards once auth is chosen (not role guards).
4. Implement feature UI + services typed against OpenAPI/DTOs. Show/hide actions with `*hasPermission`. Add a role-admin screen (catalog checkboxes → save role) for users with `roles.manage`. Prefer existing playbook components before creating a new shared primitive; if you add one, run `skills/enterprise-component-playbook.md`.
5. Handle loading, empty, and error states consistently (`skills/enterprise-angular-client-quality.md`).
6. Add unit tests for non-trivial logic; e2e smoke for critical path when tooling exists.
7. If PWA/offline is in scope, stop after the shell and hand off into `skills/enterprise-pwa-offline.md` rather than inventing a second cache layer.
8. Note build/serve commands and required env placeholders. Emit collaboration **Handoff** to Auth, .NET (contract gaps), and Testing.

## Quality bar
- No `any` without justification
- No secrets in environments committed to git
- Keyboard-accessible primary flows
- Reuse playbook components before inventing a second button/field/dialog
- Consistent error UX
- No Nx workspace unless a dedicated Nx agent owns that work
- No role-name checks in guards or templates
- New apps/features come from `ng new` / `ng generate`, not hand-written workspace files

## Report back
- Paths created/changed
- CLI used (`ng version` + commands) or fallback reason
- Serve/build commands
- API contracts assumed
- Auth approach wired (or blocked waiting on Auth)
- Permission helpers and role-admin path (or TBD)
- Playbook pages for new shared components (or N/A)
