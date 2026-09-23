---
name: Enterprise Microsoft Identity authentication
description: Use when implementing Cookie or OAuth 2.0 / OIDC authentication with ASP.NET Core Identity and/or Microsoft.Identity.Web. Do not assume the mechanism — the user must have chosen Cookie or OIDC.
---
# Enterprise Microsoft Identity authentication

## Goal
Authenticate users with Microsoft OSS only: ASP.NET Core Identity (cookies) and/or `Microsoft.Identity.Web` (OAuth 2.0 / OpenID Connect, including Microsoft Entra ID).

## When to use
After the Auth specialist asked and the user chose **Cookie-based** or **OAuth 2.0 / OIDC**. If they chose JWT / token-based, use `skills/enterprise-jwt-implementation.md` instead.

## Defaults
- Cookie: `Microsoft.AspNetCore.Identity.EntityFrameworkCore` + cookie auth. HTTP-only, Secure, SameSite appropriate for the SPA hosting model (same-site or BFF preferred over cross-site cookies)
- OIDC: `Microsoft.Identity.Web` for Entra ID / compatible OIDC providers. Validate issuer, audience, scopes
- Do **not** use Duende IdentityServer or other paid identity products. If a self-hosted OSS IdP is required later, call that out as a separate decision (e.g. OpenIddict, Keycloak)
- Authorization is a follow-on: `skills/enterprise-permission-based-access.md` and `skills/enterprise-microsoft-identity-authorization.md` (permissions, not role names)
- Angular (Ionic reuses): cookie credentials (`withCredentials`) or OIDC redirect/PKCE per the chosen approach — never put client secrets in the SPA. Do not cache login/refresh responses in the service worker (`skills/enterprise-pwa-offline.md`)
- Seed dummy users / test accounts for **Development** only
- Open-source / built-in only

## Steps
1. Confirm Cookie vs OIDC with the user's answer.
2. Add Identity and/or Microsoft.Identity.Web in the API host (or BFF if the SPA is cross-origin).
3. Configure cookie or OIDC options from configuration; no hardcoded tenant IDs or secrets.
4. Expose login/logout (and challenge/callback for OIDC). Keep the Auth surface in one module.
5. Seed Development users, the permission catalog, and a few sample roles (as permission sets). Guard seeding with environment checks.
6. Wire the Angular client (cookie interceptor or OIDC flow). Ionic reuses the same helpers. Coordinate with Angular (and Ionic if present) skills.
7. Test: login success/failure, logout, unauthenticated 401, CSRF for cookies, callback/state for OIDC.

## Quality bar
- No secrets in git or client bundles
- Cookies never work over HTTP in production
- OIDC client secrets stay on the server (BFF) if confidential client
- Seed data cannot run in production

## Report back
- Chosen approach (Cookie or OIDC)
- Paths created/changed
- How to sign in locally
- Residual risks
