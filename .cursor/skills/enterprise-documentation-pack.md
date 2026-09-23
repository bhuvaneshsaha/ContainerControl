---
name: Enterprise documentation pack
description: Use when writing or updating ADRs, READMEs, API notes, and runbooks for an enterprise .NET + Angular application (PWA and Ionic when present).
---
# Enterprise documentation pack

## Goal
Ship the minimum docs operators and future engineers need — accurate, short, living.

## Artifacts (create only what is missing)
1. **README** — purpose, prerequisites, run/migrate/test, local seed/sample-data steps, env var table (names only)
2. **ADR(s)** — non-obvious architecture decisions (including Modular Monolith vs microservices, CQRS, hosting platform, auth approach once chosen)
3. **Permissions** — catalog of permission codes; how operators create roles from them
4. **API** — OpenAPI link or summary of major resources
5. **Runbook** — deploy, rollback, common incidents
6. **Threat/security notes** — if compliance-relevant
7. **Frontend** — routes map, state ownership (signals vs store), PWA/offline ADR if present, Ionic platform targets only if Ionic exists, **link to the component playbook index** (`skills/enterprise-component-playbook.md` — do not duplicate API tables here)
8. **Modules** — bounded-context / module map for a Modular Monolith
9. **Observability** — correlation header names, health URLs, how to find a failed request (`skills/enterprise-observability.md`)

## Steps
1. Inventory existing docs; do not duplicate.
2. Prefer updating code-adjacent docs (README, `/docs`) over orphan wikis.
3. Keep examples copy-pasteable; never embed real secrets.
4. Mark unknowns as TODO with owners when known (auth approach or On-prem / Azure / AWS if still unchosen).

## Report back
- Files written/updated
- Doc gaps still open
