---
name: HasPermission
framework: angular
status: stable
source: client/src/app/shared/has-permission.ts
export: appHasPermission
---

# HasPermission

## Purpose
Shows or hides a template when the signed-in user has a permission code. The API remains the authority.

## When to use
- A nav link or button that calls an endpoint protected by the same permission code.

## When not to use
- To decide access by a role name.
- As the only check. The endpoint still returns 403.

## Public API

### Inputs / props
| Name | Type | Default | Required | Description |
|------|------|---------|----------|-------------|
| `appHasPermission` | `string` | none | yes | Permission code, for example `platform.hosts.manage`. |

### Outputs / events
| Name | Payload | Description |
|------|---------|-------------|
| None | None | None |

### Content / slots
The host element is the template. It is created when the code is present and removed when it is not.

### Configuration
Standalone directive. Import `HasPermission` on the parent component. `PermissionService` is `providedIn: 'root'` and is filled from `GET /me/permissions`.

## Variants and states
The template is either in the view or not. There is no disabled or loading variant on this directive.

## Usage

```html
<a *appHasPermission="'platform.hosts.manage'" routerLink="/hosts">Hosts</a>
```

## Accessibility
The control is removed from the accessibility tree when the permission is missing, so it is not focusable. The caller still labels the control it projects.

## Dependencies
`PermissionService`. No extra component library.

## Do
- Pass a permission code from the catalog.
- Keep the same code on the API endpoint.

## Don’t
- Pass a role name.
- Treat a hidden button as authorization.

## Common mistakes
- The link stays hidden after a role change until the user signs in again, because the cookie claims are what `/me/permissions` returns.

## Related
[Permission catalog](../../permissions.md).

## Source of truth
`client/src/app/shared/has-permission.ts`, class `HasPermission`, input `appHasPermission`.
