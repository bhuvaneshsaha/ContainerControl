---
name: Spawn enterprise specialist agent
description: Use when the master orchestrator needs a new specialized role that is not already in the roster and is not covered by a skill (PWA, EF Core, observability, Angular client quality, component playbook, and collaboration are skills — do not spawn agents for them). Later Nx is the usual exception.
---
# Spawn enterprise specialist agent

## Goal
Define one focused specialist for a gap in the .NET + Angular (optional Ionic) enterprise delivery roster — only when no existing agent prompt already covers it.

## Before creating
1. List current agent prompts under `agents/` (or your host's agent list) and reuse/refine if a close match exists. This pack does **not** keep a second copy under `.cursor/`.
2. Confirm the gap is enduring (role), not a one-off task the current chat should just do. Prefer a new **skill** for a repeatable procedure.
3. Do **not** spawn agents named Data, PWA, Offline, Observability, Accessibility, Angular-quality, or Component Playbook — those are skills owned by existing specialists (`enterprise-ef-core-data`, `enterprise-pwa-offline`, `enterprise-observability`, `enterprise-angular-client-quality`, `enterprise-component-playbook`, `enterprise-team-collaboration`).
4. Do not fold Nx into the Angular agent; if Nx is needed, spawn it as its own specialist.
5. Do not fold PWA into Ionic; Angular owns web PWA.

## Create

Write a `.md` file under **`agents/`** only. Keep YAML frontmatter so hosts that understand it can list the agent:

```markdown
---
name: enterprise-role-name
model: inherit
description: One-line role plus when to use it. Use proactively for <triggers>.
is_background: true
---
```

- **name**: lowercase letters and hyphens only (e.g. `enterprise-devops`)
- **model**: `inherit` so the agent uses the parent chat's model (works even if a specific model is unavailable)
- **description**: specific triggers; include "Use proactively" so the parent can delegate
- **is_background**: `true` for async work (CI, review, tests); `false` when the agent must ask the user (auth, architecture)

Then the markdown body is the system prompt.

## Create (other hosts)
- Write the same markdown under `agents/` (or paste as a new chat's system / custom instructions).
- **system prompt must include**:
  - Mission and lifecycle phase owned
  - Stack defaults (ASP.NET Core / EF Core / Angular / PWA skill / optional Ionic / On-prem or Azure or AWS as relevant)
  - Owns / does not own / skills / handoff (see existing specialists)
  - Enterprise quality bar (simple DDD, tests, security, observability, open-source only)
  - How they hand work back (paths, commands, open decisions)
  - Tone: concise Tech Lead peer
- In multi-agent hosts (Grok Bot, Crew-style, etc.), also register the agent via the host's create-agent API if available.

## After create
1. Open a dedicated chat/session with that prompt and give the concrete assignment.
2. Tell the user the agent name and what it will own.
3. Prefer skills for reusable procedures; agents for ongoing ownership.

## Anti-patterns
- Spawning overlapping agents
- Empty or one-line prompts
- Creating agents for tiny one-shot questions
- Adding commercial or paid product defaults
- Merging Nx into Angular
- Merging PWA into Ionic, or spawning Data/PWA/Observability/Component Playbook agents
- Duplicating `agents/` into `.cursor/agents/` in this pack
