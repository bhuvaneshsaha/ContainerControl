---
name: Enterprise security checklist
description: Use when hardening or reviewing security for a .NET + Angular enterprise app (auth, secrets, OWASP, CORS, PII, PWA/IndexedDB). Do not assume the auth mechanism. Do not implement login — that is the Auth specialist.
---
# Enterprise security checklist

## Goal
Threat-aware hardening for a .NET API + Angular client (Ionic if present, PWA if present, or BFF) before production. Auth *implementation* is out of scope — review it, do not rewrite it unless asked.

## Checklist (mark Pass / Fail / N/A with evidence)
1. **AuthN** — matches the user-chosen approach (Cookie, JWT, or OAuth 2.0 / OIDC). Cookie: secure, HTTP-only, SameSite. JWT: issuer, audience, lifetime, key handling. OIDC: validated tokens via `Microsoft.Identity.Web` or equivalent OSS. Implementation is owned by the Auth specialist.
2. **AuthZ** — endpoints and UI check **permissions**, not role names. Roles are user-composed from the catalog. Resource-based checks where needed; no client-trusted permission lists
3. **Input** — validation on all boundaries; parameterized queries / EF; no string-concat SQL
4. **Output** — encoding; CSP for Angular/Ionic; no sensitive data in logs or error bodies
5. **Secrets** — platform secret store (on-prem vault/env, Azure Key Vault, AWS Secrets Manager) or user-secrets; nothing in git or client bundles
6. **Transport** — HTTPS only; HSTS in prod; secure CORS (explicit origins)
7. **Dependencies** — known vulnerable packages flagged; lockfiles present; **no commercial/paid auth or UI products** unless the user accepted them
8. **Abuse** — rate limiting, lockout/brute-force controls on auth endpoints
9. **Data** — PII minimization; encryption at rest where required; retention notes; seed data disabled in production
10. **Supply chain** — CI scans if available; signed artifacts when applicable
11. **PWA / offline** — if a service worker exists: tokens not in Cache Storage/IndexedDB; logout clears user data; API cache is allowlisted; XSS + persisted offline PII called out. N/A if online-only SPA
12. **Observability** — logs/traces do not contain secrets or unnecessary PII (`skills/enterprise-observability.md`)

## Steps
1. Inventory trust boundaries and data classes.
2. Walk the checklist against real code/config paths.
3. Produce Critical / High / Medium findings with file or config references.
4. Propose concrete fixes; do not invent CVEs or scores.

## Report back
- Findings table
- Quick wins vs deeper work
- Residual risk accepted only if the user agrees
