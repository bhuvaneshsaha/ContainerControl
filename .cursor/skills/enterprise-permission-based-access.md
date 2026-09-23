---
name: Enterprise permission-based access
description: Use when implementing authorization for .NET + Angular (Ionic reuses the same helpers). The app checks permissions; users build roles dynamically from the permission catalog. Do not authorize by role name.
---
# Enterprise permission-based access

## Goal
Ship a small, readable permission model: the **product defines permissions**; **users compose roles** from those permissions; **backend and UI check permissions only**.

## Model (keep this obvious)
- **Permission** — stable app-defined code, e.g. `orders.read`, `orders.write`, `roles.manage`. Seeded from code; end users do not invent permission codes.
- **Role** — a named set of permissions created/edited in the app (Admin UI). Not hardcoded in `[Authorize(Roles = ...)]`.
- **Assignment** — users (or groups) get one or more roles. Effective permissions = union of those roles' permissions.
- **Check** — API and UI ask "does the user have permission X?", never "is the user in role Y?".

Roles exist so operators can create "Warehouse clerk" or "Finance approver" without a deploy. New roles must not require new code — only new rows linking existing permissions.

## Backend (Auth module)
- Catalog: a static list or seeded table of permissions (code, display name, module).
- Entities: `Role`, `RolePermission`, `UserRole` (simple; no extra DDD ceremony).
- APIs (permission-gated themselves, typically `roles.manage`):
  - `GET /permissions` — catalog for the role editor
  - `GET/POST/PUT/DELETE /roles` — CRUD roles as permission sets
  - assign/unassign roles to users
  - `GET /me/permissions` — effective permissions for the current user
- ASP.NET: custom `HasPermission` requirement / policy (or a tiny `[HasPermission("orders.read")]` attribute). **Do not** use `[Authorize(Roles = "Admin")]`.
- Put **permission codes** on the `ClaimsPrincipal` after login (from the user's roles) so endpoint checks stay cheap. Refresh claims when roles change (re-issue token or reload cookie principal).
- Resource-based checks (`IAuthorizationHandler`) still apply when ownership matters, *in addition to* the permission.
- Seed Development users, a few sample roles, and the full permission catalog. Never seed production roles with extra privilege by accident.
- Open-source / built-in only (`Microsoft.AspNetCore.Authorization`). Follow `skills/enterprise-microsoft-identity-authorization.md` for how policies are registered.

Do not duplicate this model in a second skill. `skills/enterprise-microsoft-identity-authorization.md` is only the ASP.NET Core policy registration.

## Frontend (Angular; Ionic reuses this)
- Load effective permissions once after login (`/me/permissions` or claims).
- Helpers: `hasPermission(code)`, `hasAny(codes)` — one injectable service.
- Route `canActivate` guard on permissions, not role names.
- Structural directive (e.g. `*hasPermission="'orders.write'"`) to show/hide actions. UI hide is UX only; the API is the source of truth.
- **Role admin screen**: list catalog permissions (grouped by module), create/edit a role by checking permissions, save. Usable by anyone with `roles.manage`. Keep it a straightforward form — no generic ACL designer.
- Dummy catalog/roles allowed in local development.

## Quality bar
- No hardcoded role names in controllers, guards, or templates
- Permission codes are part of the module's ubiquitous language
- Creating a role never requires a code change
- Adding a new product capability *does* require a new permission in the catalog and a check at the endpoint/UI
- Least privilege; deny by default (401 unauthenticated, 403 missing permission)

## Tests
- User with permission allowed; same user without it 403
- Role CRUD: new role with a subset of permissions takes effect without restart
- UI: action hidden without permission; API still rejects if called
- Do not assert on role names

## Report back
- Permission catalog
- Role admin paths (API + UI)
- How permissions attach to the principal
- Sample seeded roles (Development)
