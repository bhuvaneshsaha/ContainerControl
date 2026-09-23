---
name: Enterprise PWA and offline
description: Use when the Angular (or Ionic) client must be installable, cacheable, or offline-capable — service workers, caching, IndexedDB, and sync with the .NET API. Not a substitute for the Ionic skill.
---
# Enterprise PWA and offline

## Goal
Ship an installable Progressive Web App on the Angular client using **built-in Angular PWA** plus a small, explicit offline/sync design. Backend sync is owned by the .NET API specialist; this skill is the shared contract.

## When to use
Architecture (or the user) asked for installable web, offline-capable screens, or background refresh. **Do not use Ionic just to get a PWA.** Ionic uses this skill only when the shell is already Ionic.

If the app is online-only SPA, skip this skill; Angular scaffold is enough.

## Ownership

| Piece | Owner |
|-------|--------|
| `ng add @angular/pwa`, `ngsw-config.json`, web app manifest | Angular |
| Runtime caching policy (assets vs API) | Angular, reviewed by Security |
| IndexedDB schema, outbox, conflict UX | Angular (Ionic reuses the same services) |
| Sync/delta/version HTTP APIs, concurrency tokens | .NET API + EF Core skill |
| Online vs offline product rules | Architecture ADR |
| Token storage vs cache | Auth + Security |
| CI headers, SW update, static hosting | DevOps |

## Defaults (override only with an ADR)

- **Online-first, offline-capable** unless Architecture says offline-first (field work, poor network).
- Service worker: **`@angular/service-worker`** (`ng add @angular/pwa`). Do not add Workbox unless Angular SW cannot express the cache plan.
- Manifest: name, icons, `display: standalone`, `theme_color`, `start_url`.
- Asset cache: hashed Angular output (prefetch or lazy per `ngsw-config`).
- API cache: **opt-in per GET resource**. Never cache authenticated mutations. Never put access tokens in Cache Storage or IndexedDB if memory/BFF is viable.
- Client store: IndexedDB via the platform API, or the small OSS wrapper `idb` if it removes boilerplate. Dexie only if the offline model is large. No commercial sync (no paid Realm/Couchbase/AppSync-style products).
- Sync: client **outbox** of mutations + server **delta** (`since` cursor / `rowversion` / ETag). Default conflict policy: **last-write-wins with `rowversion`** and a user-visible conflict when the domain cannot lose data (money, inventory).
- Idempotency: clients send an idempotency key on outbox POSTs; API stores it.
- Push is optional: `Microsoft.AspNetCore.SignalR` (OSS) or platform push later — not required for v1.
- Open-source or built-in only.

## Steps

1. Confirm scope with Architecture: installable only vs cached shell vs true offline mutations. Write or update a short ADR.
2. Angular: `ng add @angular/pwa`. Set `ngsw-config.json` data groups **explicitly** (not “cache everything”).
3. Auth: service worker must not cache `/me`, login, refresh, or tokens. Align with JWT vs Cookie vs OIDC skill.
4. IndexedDB: one database per app, object stores per offline module (mirror bounded contexts). Store entities + `updatedAt`/`version` + outbox records. Encryption at rest is not default; if PII is stored offline, Security must accept residual risk.
5. .NET: add sync endpoints in the owning module (not a generic catch-all):
   - `GET .../changes?since=` (or ETag / `If-None-Match`)
   - `POST` mutations that honor idempotency keys and EF concurrency tokens (`enterprise-ef-core-data.md`)
   - 409 + current resource on conflict
6. UI: offline banner, queued-outbox indicator, conflict resolution screen only where LWW is unsafe.
7. Tests: SW config is present; outbox retries; 409 path; “offline then sync” component or integration test. E2E one journey with network throttling if Playwright exists.
8. DevOps: correct cache headers for `ngsw.json` / `index.html` (always revalidate HTML and ngsw metadata; long-cache hashed assets). Platform skill for where the SPA is hosted.

## Quality bar

- App is installable on desktop and mobile browsers that support PWA (evidence: manifest + SW registration, not a screenshot claim).
- Logging out clears or isolates cached user data.
- No secrets in the SW or IndexedDB.
- Sync is module-owned, not a second monolith database API.
- Ionic must not invent a parallel IndexedDB layer; import Angular offline services.

## Report back

- PWA scope (installable / cached / offline mutations)
- `ngsw-config` data groups
- IndexedDB stores and outbox shape
- Sync API paths and conflict policy
- Residual risks (XSS + persisted data, quota, SW update)
- Handoff to Auth, Security, DevOps, Testing as needed
