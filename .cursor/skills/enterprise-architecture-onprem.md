---
name: Enterprise architecture on-premises
description: Use when the architecture specialist is targeting on-premises or self-hosted datacenter deployment (VMs, IIS/Kestrel, local SQL, no public cloud).
---
# Enterprise architecture on-premises

## Goal
Map a Modular Monolith .NET + Angular (PWA if chosen; Ionic only if native shell) system onto on-premises hosting without pulling in Azure or AWS services.

## When to use
The user chose **on-premises** / self-hosted / private datacenter, or the constraints forbid public cloud. If the target is Azure or AWS, stop and use that platform skill instead.

## Defaults
- Compute: Kestrel behind IIS, nginx, or a reverse proxy on Windows/Linux VMs; containers (Docker) optional if the ops team already runs them
- Data: SQL Server or PostgreSQL on-prem; files on local/NAS storage
- Secrets: environment variables, OS-protected files, or HashiCorp Vault (OSS) — not a cloud KMS unless already present
- Identity hosting: app-owned auth (Cookie / JWT / OIDC per Auth specialist). LDAP/AD as an OIDC/LDAP source only if the user asks
- Observability: structured logs to files or an OSS stack (e.g. OpenTelemetry + on-prem collector). Follow `skills/enterprise-observability.md`
- Frontend: serve the Angular SPA/PWA from the reverse proxy or the API host (same-site cookies are easier on-prem). Ionic only if Architecture chose a native shell
- PWA: reverse proxy must not aggressively cache `index.html` or `ngsw.json`. Follow `skills/enterprise-pwa-offline.md` when the client is a PWA
- CI/CD: self-hosted runners (GitHub Actions, Azure DevOps Server, GitLab, Jenkins) — pick what the user already has
- Open-source or built-in only — no commercial appliances as a default

## Steps
1. Confirm network zones (DMZ vs internal), TLS termination, and whether the SPA is same-site or needs a BFF.
2. Size a single Modular Monolith host first; split only if an existing constraint requires it.
3. Plan backup, patch windows, and how migrations run (outage vs expand/contract).
4. Environments: at least Dev (local seed data), Test, Prod — no cloud-specific names.
5. Write a short ADR: why on-prem, what is explicitly out of scope (Azure/AWS).

## Report back
- Host, data, secrets, and CI outline
- Ingress / TLS / CORS implications for Angular (and Ionic if present)
- Risks (capacity, HA, patching)
