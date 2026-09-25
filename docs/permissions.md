# Permission catalog

Operators compose roles from these codes. The API and the Angular app check the code. They do not check a role name. Identity's built-in role table is not the authorization model.

A new product capability needs a new code in `PermissionCatalog` and a check at the endpoint. A new role is a row, not a code change. After a role change, the user signs in again so the cookie picks up the new codes. A break-glass grant is not copied onto that cookie. The permission check reads an unexpired grant on each call, and `/auth/session` includes it. The grant lasts 5 to 60 minutes.

| Code | Module |
| --- | --- |
| `access.users.read` | Access |
| `access.users.manage` | Access |
| `access.teams.manage` | Access |
| `access.roles.manage` | Access |
| `access.tokens.manage` | Access |
| `access.breakglass.grant` | Access |
| `access.audit.read` | Access |
| `platform.hosts.manage` | Platform |
| `platform.quotas.manage` | Platform |
| `platform.settings.manage` | Platform |
| `platform.capacity.read` | Platform |
| `registries.read` | Registries |
| `registries.manage` | Registries |
| `apps.read` | Applications |
| `apps.write` | Applications |
| `secrets.read` | Secrets |
| `secrets.manage` | Secrets |
| `secrets.manage.prod` | Secrets |
| `deploy.execute` | Delivery |
| `deploy.approve` | Delivery |
| `deploy.rollback` | Delivery |
| `edge.certs.manage` | Edge |
| `edge.dns.manage` | Edge |
| `runtime.logs.read` | Runtime |
| `runtime.stats.read` | Runtime |
| `runtime.control` | Runtime |

`GET /permissions` returns the catalog for a caller with `access.roles.manage`. `GET /me/permissions` returns the signed-in user's codes. Role create, update, and delete use `access.roles.manage`. User create, disable, and role assignment use `access.users.manage`. `apps.write` creates, updates, and removes applications. The Applications page shows Edit and Remove only for that code. `deploy.execute` also starts a second slot beside the live release (`POST /apps/{id}/slots/deploy`), swaps public traffic onto that slot (`POST /apps/{id}/slots/swap`), and sets a canary percent (`POST /apps/{id}/slots/canary`). `deploy.rollback` also sends public traffic back to the previous slot (`POST /apps/{id}/slots/revert`). `deploy.approve` accepts a deploy that is waiting, including a slot deploy that was parked. The Applications page shows Approve only for that code. `platform.settings.manage` reads and saves the alert webhook and mail recipients at `GET` and `PUT /platform/alerts`. `runtime.logs.read` also reads stored lines at `GET /apps/{id}/logs/stored`. `access.audit.read` calls `GET /access/audit`. `access.roles.manage` edits roles from the catalog checkboxes on the Access page. `access.breakglass.grant` calls `POST /access/break-glass`. That writes an audit row and does not open a shell or the Docker socket.

Development sample roles, created only when `ASPNETCORE_ENVIRONMENT` is `Development`:

- Platform administrator: every catalog code.
- Developer: `apps.read`, `apps.write`, `secrets.read`, `secrets.manage`, `deploy.execute`, `deploy.rollback`, `registries.read`, `runtime.logs.read`, `runtime.stats.read`, `runtime.control`.

The developer role does not include `platform.hosts.manage`. The Hosts nav item uses that same code.
