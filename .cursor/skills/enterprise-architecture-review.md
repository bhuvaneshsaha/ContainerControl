---
name: Enterprise architecture review
description: Use when designing or reviewing solution architecture for a .NET + Angular enterprise system (simple DDD, Modular Monolith, APIs, data, PWA/offline, hosting). Load On-prem, Azure, or AWS platform skill for hosting. Ionic only if native shell is in scope.
---
# Enterprise architecture review

## Goal
Produce a concrete architecture decision set for an enterprise .NET + Angular system that stays simple and extractable. PWA/offline is a client capability; Ionic is optional native shell — not the default web architecture.

## Owns / does not own
- **Owns:** bounded contexts, Modular Monolith map, ADRs, hosting platform, online vs offline, SQL vs document per module, REST vs async integration.
- **Does not own:** Cookie vs JWT vs OIDC (Auth asks); writing module or SPA code; pipeline YAML (DevOps); threat-model evidence (Security).

Follow `skills/enterprise-team-collaboration.md` for sequence and handoff. Persistence details: `skills/enterprise-ef-core-data.md`. Observability backends: `skills/enterprise-observability.md` plus the platform skill.

## Steps
1. Capture goals, users, SLAs, compliance, and constraints.
2. Draw bounded contexts and ownership boundaries (ubiquitous language; one module per context).
3. Default the backend to a **Modular Monolith** when the app is enterprise-level or expected to grow: in-process modules, small shared kernel, clear public contracts. Keep boundaries so a module can later become a microservice.
4. Apply simple DDD: entities, value objects, aggregates only when invariants need them. Skip repositories-for-everything, domain-event storms, sagas, and specification-pattern defaults.
5. Choose sync vs async integration; API style (REST default). Do **not** choose Cookie vs JWT vs OIDC — the Auth specialist asks the user.
6. Authorization: permission catalog + user-composed roles — follow `skills/enterprise-permission-based-access.md`. Do not design role-name checks.
7. Data: SQL vs document stores per module; consistency and migration strategy; prefer module-owned schemas/tables in one database until a split is justified.
8. **Frontend:** Angular SPA is the default web client. Ask whether the web app must be **installable PWA** and whether any screens are **offline-capable** (online-first vs offline-first). If yes, ADR + `skills/enterprise-pwa-offline.md` (Angular owns the client; .NET owns sync APIs). **Ionic/Capacitor only** when native mobile or desktop shell is required — not in order to “get PWA.” No Nx unless a dedicated Nx agent is in play.
9. CQRS only when reads and writes genuinely diverge — follow `skills/enterprise-cqrs.md`; never MediatR or similar libraries.
10. Cross-cutting: logging, tracing, config, feature flags, caching, rate limits — built-in or OSS. Name the observability backend; do not pick a paid APM.
11. **Hosting:** if On-premises / Azure / AWS is not stated, ask. Then follow exactly one of:
    - `skills/enterprise-architecture-onprem.md`
    - `skills/enterprise-architecture-azure.md`
    - `skills/enterprise-architecture-aws.md`
12. Write short ADRs for non-obvious decisions (Context / Decision / Consequences), including hosting, PWA/offline, and CQRS yes/no.

## Output format
- Context diagram (mermaid if helpful)
- Modular Monolith module map with responsibilities
- Client shape: Angular SPA / Angular PWA / Ionic shell (why)
- Platform skill used (On-prem / Azure / AWS)
- ADR bullets
- Risks and open questions (include "auth approach TBD" and "platform TBD" until answered)
- Suggested specialist agents / skills to execute next
- Handoff block from `skills/enterprise-team-collaboration.md`

## Rules
- Prefer boring, proven patterns over novelty
- Open-source or built-in only — no commercial products
- Do not assume Azure (or any cloud)
- Do not assume Ionic for PWA
- State assumptions explicitly
- Do not invent vendor pricing or unverified latency numbers
- Keep DDD readable for developers new to it
