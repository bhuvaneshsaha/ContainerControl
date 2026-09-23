---
name: Enterprise Microsoft Identity authorization
description: Use when wiring ASP.NET Core authorization policies (HasPermission) after authentication is chosen. This is the ASP.NET adapter, not a second authZ model — the product model is enterprise-permission-based-access. Complements Cookie, JWT, or OIDC.
---
# Enterprise Microsoft Identity authorization

## Goal
Register ASP.NET Core authorization so endpoints check **permissions**. Roles are data (permission sets), not policy names.

## When to use
After the user chose Cookie, JWT, or OIDC and authentication is (or will be) wired. Pair with `skills/enterprise-permission-based-access.md` for the catalog, role CRUD, and Angular UX (Ionic reuses it). **Do not invent a parallel policy model** — this skill only registers how ASP.NET Core evaluates permission codes. This skill does not choose authentication.

## Defaults
- `[HasPermission("orders.read")]` or a named policy per permission code — never `[Authorize(Roles = "Admin")]`
- Effective permissions come from the user's assigned roles (union); they are flattened onto the `ClaimsPrincipal` (claim type e.g. `permission`)
- Resource-based authorization (`IAuthorizationHandler`) when access depends on the entity, in addition to the permission
- Never trust permission or role lists sent only from the client
- Keep policy registration in the Auth module so feature modules stay simple
- Open-source / built-in only (`Microsoft.AspNetCore.Authorization`)

## Steps
1. Confirm the permission catalog with `skills/enterprise-permission-based-access.md`.
2. Register a single permission requirement/handler (parameterized by permission code), not one policy class per role.
3. Apply permission checks at endpoints/controllers, not deep in domain services unless a domain invariant requires it.
4. Add resource handlers only where a permission is not enough (e.g. owner of a record).
5. Deny by default: unauthenticated → 401, authenticated but missing permission → 403.
6. Tests: allowed permission, missing permission, resource owner vs stranger, anonymous. Do not assert role names.
7. Document the permission catalog next to the code; document that roles are managed at runtime.

## Quality bar
- Least privilege; no role-name bypass
- Permission codes in ubiquitous language
- No authorization logic duplicated in Angular as the source of truth (UI may hide actions; API must enforce)
- Creating a new role in the UI does not require a code change

## Report back
- How `HasPermission` is registered
- Claim type used for permissions
- Paths created/changed
- Gaps (e.g. missing resource checks)
