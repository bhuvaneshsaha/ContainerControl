---
name: Enterprise test strategy
description: Use when defining or implementing unit, integration, contract, and e2e tests for .NET + Angular enterprise apps (Ionic/PWA when present). Specialists write the first tests; this skill owns the pyramid and gaps.
---
# Enterprise test strategy

## Goal
Define and implement a pragmatic test pyramid for a .NET + Angular enterprise system (Ionic shell and PWA/offline when in scope).

## Ownership
- Feature specialists write the first unit/integration tests for code they add.
- **Testing specialist** owns the matrix, CI-friendliness, contract tests, lean e2e, permission-denial suite, and filling gaps.
- Never invent coverage % or claim green runs without evidence.

## Defaults
- Domain/unit tests: fast, no I/O
- Application tests: handlers/services with fakes
- API integration: WebApplicationFactory + **Testcontainers** or LocalDB — not EF InMemory for persistence/concurrency/PWA version tests (`skills/enterprise-ef-core-data.md`)
- Angular: unit tests for mapping/state/pipes/services; component tests for critical UI and permission directive
- Accessibility: at least one primary flow checked for labels/keyboard (component test or Playwright a11y snapshot if present)
- Ionic: only extra tests for shell/navigation/platform when Ionic exists
- PWA: outbox retry, 409 conflict, “offline then sync” when `skills/enterprise-pwa-offline.md` was applied
- E2E: few high-value journeys only (Playwright preferred OSS; Cypress if already in repo)
- Contract tests when multiple modules or a generated OpenAPI client share APIs
- Dummy/sample seed data and fixtures for local and test environments; never production
- Open-source test libraries only — no paid device clouds as a default

## Steps
1. List critical user journeys and failure modes (include permission denials and, if PWA, sync/conflict).
2. Map each to unit / integration / e2e **and** which specialist already owns the first test.
3. Add missing high-value tests first (auth, money/state transitions, **permission** denials — not role-name checks, module boundaries, role-admin creating a role that grants a permission, PWA conflict if in scope).
4. Ensure tests are deterministic and CI-friendly (no wall-clock flakiness).
5. Report coverage gaps qualitatively — never invent coverage percentages. Emit collaboration **Handoff** to DevOps for CI commands.

## Report back
- Test matrix (journey → layer → owner → status)
- Commands to run
- Gaps and risks
