# ADR 0010: ASP.NET Core Identity cookies, admin-provisioned users, API tokens for CI

## Status
Accepted

## Context
Sign-in must work without Entra ID in v1. Users are created by an administrator. CI calls the deploy webhook with a bearer token. Authorization is permission-based, not Identity's built-in role table.

## Decision
Use ASP.NET Core Identity on the same PostgreSQL database with an HTTP-only SameSite cookie. The browser sends it with `withCredentials`. There is no self-registration. The first administrator is created from host environment variables only when the database has no users. Development seeds sample users. CI API tokens are hashed in PostgreSQL. Entra ID linking with `Microsoft.Identity.Web` is deferred and is not a v1 dependency. Checks use permission codes only.

## Consequences
`POST /access/tokens` issues a token for a caller with `access.tokens.manage`. The response returns the plaintext once. PostgreSQL stores the hash. `POST /delivery/webhook` accepts that token as a Bearer credential and still requires `deploy.execute`. A role change is visible after the user signs in again, because the cookie carries the permission claims. Operators must not turn on self-registration or run the Development seed in production. Entra ID linking remains deferred.
