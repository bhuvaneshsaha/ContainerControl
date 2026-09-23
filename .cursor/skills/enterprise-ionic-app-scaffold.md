---
name: Enterprise Ionic app scaffold
description: Use when the client must use the Ionic shell and/or Capacitor for native mobile or desktop. Do not use this skill merely to add a PWA — that is Angular + the PWA skill. Delegates Angular and PWA work to those skills.
---
# Enterprise Ionic app scaffold

## Goal
Produce a production-ready Ionic + Angular **shell** when native mobile/desktop (Capacitor) or Ionic UI is required. Web PWA/offline is not a reason to start here — use Angular + `skills/enterprise-pwa-offline.md`.

## Composition
- Ionic owns: app shell, `ion-*` layout, navigation, platform detection, Capacitor config placeholders
- Angular owns: TypeScript, standalone features, routing internals, forms, HTTP, unit tests, permission helpers — follow `skills/enterprise-angular-app-scaffold.md`, `skills/enterprise-angular-client-quality.md`, and `agents/03-angular.md`
- PWA/offline: reuse `skills/enterprise-pwa-offline.md` (same SW/IndexedDB/outbox services). Do not create a second cache/sync stack
- Auth waits on `agents/08-auth.md` (Cookie / JWT / OIDC)
- Permissions: reuse Angular permission service/guard/directive; never check role names

## Defaults
- Ionic (OSS) with Angular; standalone components
- Targets: Ionic shell on desktop and mobile browsers; Capacitor Android/iOS documented as **next** unless the user asks to implement them
- If the product also needs a PWA, apply the PWA skill on this Angular workspace — do not add a second app
- Responsive layout that works at desktop and phone widths
- Dummy/sample data allowed for local development
- Open-source only — no commercial Ionic plugins or UI kits

## CLI first (do not hand-write a new app)
Ionic CLI produces the correct Angular+Ionic workspace, Capacitor config, and scripts. Do not invent that tree file-by-file.

1. **Check:** `ionic --version` (needs Node.js LTS). Capacitor CLI comes with the template.
2. **If present:** `ionic start` with `--type=angular` and a starter that fits desktop+mobile (e.g. `sidemenu` or `tabs`). Use `ionic generate` / Angular `ng generate` for pages and services. Follow `skills/enterprise-angular-app-scaffold.md` for Angular-side generates. Enable PWA only via `skills/enterprise-pwa-offline.md` if Architecture asked for installable/offline.
3. After CLI output exists, add navigation, permission helpers, Tailwind decision — **do not recreate** `ionic.config.json` / `capacitor.config` / `package.json` by hand.
4. **If Ionic/Node is missing:** stop. Tell the user to install Node.js LTS and `@ionic/cli` (`npm i -g @ionic/cli`). Ask whether to wait. Hand-write the shell **only if the user explicitly agrees to a no-CLI fallback**; say so in the report.

## Tailwind CSS — evaluate, then decide
Tailwind is a good fit when you need utility layout around Ionic pages **and** you disable Tailwind preflight so it does not reset `ion-*` styles. Use Ionic CSS variables / utility classes for components; use Tailwind for spacing, grids, and custom (non-`ion-*`) UI.

Skip Tailwind when Ionic's own CSS variables and utilities already cover the design, or when two design systems would confuse newcomers.

Record the decision in the report-back.

## Steps
1. Confirm that Ionic/Capacitor is actually required (not just “we want a PWA”). Confirm API contracts and Auth choice before guards/interceptors.
2. Check Ionic CLI; create the app with `ionic start`; reuse Angular `ng generate` for features.
3. Add shell + navigation (tabs or side menu) that works on desktop and mobile.
4. If PWA/offline is in scope, follow `skills/enterprise-pwa-offline.md` (never cache auth tokens).
5. Add Capacitor config comments/placeholders for future Android/iOS (app id, platforms) without requiring native toolchains now.
6. Apply the Tailwind decision (configured without fighting Ionic, or explicitly skipped).
7. Handle loading, empty, and error states; keyboard/touch + desktop pointer (`skills/enterprise-angular-client-quality.md`).
8. Note `ionic serve` / build commands and env placeholders. Shared shell controls: `skills/enterprise-component-playbook.md`. Emit collaboration **Handoff**.

## Quality bar
- Primary flows usable with keyboard on desktop and touch on mobile
- No secrets in client bundles
- No Nx unless a dedicated Nx agent owns the workspace
- Ionic and Angular conventions stay consistent
- New apps come from `ionic start`, not hand-written Ionic config

## Report back
- Paths created/changed
- CLI used (`ionic --version` + commands) or fallback reason
- Serve/build commands
- Why Ionic was used (native/shell) vs Angular PWA alone
- Platform targets (what's live vs future Android/iOS)
- Tailwind: used / skipped, and why
- PWA skill applied or N/A
- Auth approach wired (or blocked waiting on Auth)
