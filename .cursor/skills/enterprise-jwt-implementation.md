---
name: Enterprise JWT implementation
description: Use when the user chose JWT / token-based authentication for a .NET API and Angular client (Ionic reuses the interceptor). Do not use until that choice is confirmed.
---
# Enterprise JWT implementation

## Goal
Implement JWT bearer authentication with ASP.NET Core built-ins — no MediatR, no paid IdP, no commercial JWT libraries.

## When to use
Only after the Auth specialist asked and the user chose **JWT / token-based** authentication. If they chose Cookie or OIDC, stop and use the Microsoft Identity authentication skill instead.

## Defaults
- `Microsoft.AspNetCore.Authentication.JwtBearer` (framework)
- Validate issuer, audience, lifetime, signing key
- Signing key from configuration / Key Vault / user-secrets — never committed
- Short-lived access tokens; refresh tokens only if the user needs them (store hashed server-side, rotate, revoke)
- Authorization: flatten the user's **permissions** into token claims; still use `skills/enterprise-microsoft-identity-authorization.md` and `skills/enterprise-permission-based-access.md`. Do not put role names in `[Authorize]` or as the only claim the API checks
- Angular (Ionic reuses): attach `Authorization: Bearer` via interceptor; never persist tokens in `localStorage` if a more secure option exists (memory + refresh, or BFF). If you must persist, document the XSS risk. Service workers and IndexedDB must not store access tokens (`skills/enterprise-pwa-offline.md`)
- Open-source / built-in only

## Steps
1. Confirm the user chose JWT.
2. Add JWT bearer authentication in the API host; bind options via the options pattern.
3. Issue tokens from a dedicated auth endpoint in an Auth module (login, refresh, logout/revoke). Keep issuance logic small and readable.
4. Seed dummy users for **Development** only (never production).
5. Wire the Angular interceptor (Ionic reuses it) and 401 retry/logout behavior.
6. Add tests: valid token, expired token, wrong audience, missing token, permission denial.
7. Document env var **names** (not values) and local login with seeded users.

## Quality bar
- No secrets in source or client bundles
- Algorithms and validation parameters explicit (do not leave defaults that skip audience/issuer)
- HTTPS in non-local environments
- Refresh tokens not logged

## Report back
- Paths created/changed
- Token lifetimes and storage choice
- How to authenticate locally with seeded users
- Residual risks
