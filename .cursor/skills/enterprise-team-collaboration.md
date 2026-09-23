---
name: Enterprise team collaboration
description: Use at the start of a product or feature, and whenever work moves between specialists. Defines roster, sequence, parallel work, and the handoff contract for the .NET + Angular + PWA enterprise team.
---
# Enterprise team collaboration

## Goal
Run the specialists as one engineering team: clear owners, explicit handoffs, no duplicate implementation, no silent guesses.

## When to use
Master loads this first. Every specialist uses the **Handoff** section when finishing or blocking. Do not spawn a new agent for a concern that already has an owner or a skill.

## Roster (do not duplicate)

| Role | Owns | Does not own |
|------|------|----------------|
| **Master** | Goal, roster, sequence, integration, quality bar | Specialist implementation when that agent exists |
| **Architecture** | Bounded contexts, Modular Monolith map, ADRs, hosting platform, online vs offline | Cookie vs JWT vs OIDC; writing module code; pipelines |
| **.NET API** | ASP.NET Core host, modules, EF Core, REST, OpenAPI, health | Auth mechanism choice; SPA/PWA UI; CI YAML |
| **Angular** | Angular SPA, state, HTTP, a11y, performance, **web PWA/offline client**, shared-component playbook pages | Nx; Ionic/Capacitor shell; API/persistence implementation; playbook website |
| **Ionic** | Ionic shell, `ion-*` layout, Capacitor/native-ready concerns | Angular internals; PWA as the default web path (that is Angular + PWA skill) |
| **Auth** | Cookie / JWT / OIDC implementation after the user chooses; permission catalog wiring | General OWASP/CORS/CSP; choosing hosting |
| **Testing** | Test strategy, pyramid, CI-friendliness, gap-fill, permission-denial suites | Feature implementation; being the only test author |
| **Security** | Threat model, hardening review, secrets/CORS/CSP/OWASP evidence | Implementing login; writing product features |
| **Code review** | PR quality gate after changes | Designing the system; implementing the fix (unless asked) |
| **Documentation** | README, ADRs as records, runbooks, permission catalog docs, component-catalog **index** | Inventing architecture, APIs, or component props |
| **DevOps** | CI/CD, containers, env promotion, pipeline secrets, health/OTel exporters | Choosing On-prem vs Azure vs AWS; app business logic |

**Skills are procedures. Agents are ongoing owners.** PWA/offline, EF Core, observability, Angular client quality, and the component playbook are skills — not extra agents.

## Default delivery sequence (greenfield)

1. **Master** — assumptions, roster, this skill.
2. **Architecture** — ask hosting (On-prem / Azure / AWS) and whether the client is online-first, installable PWA, and/or offline-capable. ADRs + module map. Skill: `enterprise-architecture-review.md` + one platform skill.
3. **Auth** — ask Cookie vs JWT vs OIDC; do not implement until answered. Can start as soon as Architecture exists.
4. **.NET API** — host + modules from the map; permission placeholders; OpenAPI. Skills: `enterprise-net-api-scaffold.md`, `enterprise-ef-core-data.md`.
5. **Angular** — app shell against API contracts. Skills: `enterprise-angular-app-scaffold.md`, `enterprise-angular-client-quality.md`. Shared UI primitives: `enterprise-component-playbook.md` (docs only).
6. **Auth implements** — then .NET + Angular wire interceptors/guards. Skills: JWT or Microsoft Identity authN + `enterprise-permission-based-access.md` + `enterprise-microsoft-identity-authorization.md`.
7. **PWA** — if installable/offline was chosen. Angular owns the client; .NET owns sync/version APIs. Skill: `enterprise-pwa-offline.md`.
8. **Ionic** — only if Capacitor / Ionic design system / native mobile or desktop shell is required. Skills: `enterprise-ionic-app-scaffold.md` + Angular + PWA skills as needed.
9. **Testing** — strategy from journeys; specialists already wrote tests for their code; Testing fills gaps. Skill: `enterprise-test-strategy.md`.
10. **Security** — threat model + checklist against real paths. Skill: `enterprise-security-checklist.md`.
11. **DevOps** — pipelines matching the platform skill. Skills: `enterprise-devops.md` + platform skill.
12. **Code review** — quality gate on the change set. Skill: `enterprise-code-review.md`.
13. **Documentation** — living; may start after Architecture. Skill: `enterprise-documentation-pack.md`.

Observability is wired by .NET + Angular as they build, and by DevOps in the pipeline — skill: `enterprise-observability.md`. Do not wait until the end.

## Parallel work (allowed)

- Documentation drafts from Architecture ADRs
- Test strategy from journeys + module map
- Threat model from Architecture (before code exists)
- DevOps Dockerfiles once a host project exists
- Auth *question* in parallel with Architecture; Auth *implementation* only after the user answers

## Do not parallel

- Angular auth guards/interceptors before AuthN choice
- PWA token caching or IndexedDB encryption guesses before Security has a chance to constrain them
- Ionic as a second SPA while Angular is still the web client — compose, do not fork
- A second cloud or IdP because a specialist prefers it

## Who writes tests

- The specialist who writes the code writes the first tests (unit + the one integration/component test that proves the slice).
- Testing specialist owns the matrix, determinism, CI shape, contract tests, lean e2e, and permission-denial coverage.
- Never claim a suite is green without running it or pointing at CI evidence.

## Handoff (required)

When a specialist finishes, blocks, or transfers work, emit:

```
## Handoff
- From: <agent>
- To: <agent or Master>
- Status: complete | blocked | needs decision
- Artifacts: <paths>
- Contracts: OpenAPI/DTO names, permission codes, env var names (not values), module public APIs, sync cursor if PWA
- Decisions made:
- Open questions (owner):
- Next: <agent> + <skill>
```

Do not dump a second copy of the implementation in the handoff. Point at files.

## Slice definition of done

A vertical slice is not done until:

- API enforces a permission; UI hides the action with the same permission code
- Persistence/migrations exist if the slice stores data
- Tests at the layer the risk deserves
- Correlation id / structured log for the use case
- No secrets in source or client bundles
- OpenAPI or typed contract updated
- README/env names updated if an operator must do something new
- New or changed **shared** UI components have a playbook page in sync with source (`skills/enterprise-component-playbook.md`)

## Anti-patterns

- Two agents implementing the same interceptor, DbContext, or pipeline
- Angular checking role names because the API is not ready — wait or stub permissions, do not invent a second authZ model
- Treating Ionic as the only PWA path
- Spawning Data, PWA, Observability, Component Playbook, or Nx agents when the skills/roster already cover them (Nx only if the repo actually needs Nx)
- Premium/commercial libraries to fill a gap (see shared quality bar)
