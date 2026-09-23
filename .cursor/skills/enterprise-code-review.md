---
name: Enterprise code review
description: Use when reviewing a PR or change set for enterprise .NET, Angular, and/or Ionic code quality, security, and production readiness.
---
# Enterprise code review

## Goal
Production-minded review of a PR or change set for .NET, Angular, and/or Ionic.

## Review lenses (priority order)
1. Correctness and edge cases
2. Security and data leaks
3. API/contract compatibility and migrations
4. Performance footguns (N+1, unbounded queries, sync-over-async)
5. Commercial or paid dependencies (UI, APM, identity, plugins — reject unless the user already accepted them)
6. DDD over-engineering and MediatR-like mediator libraries
7. Authorization by role name (`[Authorize(Roles = ...)]`, `isAdmin` in templates) instead of permissions
8. Maintainability and naming within existing patterns
9. Tests adequacy for the risk of the change
10. Observability (logs/metrics meaningful? secrets in logs?)
11. Hand-written project/workspace files (`.csproj`, `angular.json`, Ionic config) when `dotnet` / `ng` / `ionic` could have scaffolded them
12. PWA: tokens in Cache Storage/IndexedDB; unbounded API cache; missing concurrency on sync APIs
13. Duplicate ownership (second DbContext style, second permission model, Ionic-only PWA stack beside Angular SW)
14. Shared UI: public component API changed without a playbook update (`skills/enterprise-component-playbook.md`)

## Steps
1. Read the PR description and linked issue/ADR.
2. Inspect diff with focus on boundaries (API, DB, auth, module edges, UI state).
3. Run or note missing tests; do not claim green CI without evidence.
4. Leave findings as Critical / High / Medium / Nit with path references.
5. Explicitly call out what looks solid.

## Rules
- Prefer actionable fixes over style debates when conventions are unset
- No bikeshedding on formatting if a linter owns it
- Never invent benchmark numbers
- Flag seed data that would run in production
- AuthN changes must match the user-chosen approach (Cookie / JWT / OIDC)
- AuthZ must check permissions; role CRUD must not require a code change for new role names

## Report back
- Verdict: approve / request changes / needs discussion
- Ordered findings
- Suggested follow-up skills or specialists
