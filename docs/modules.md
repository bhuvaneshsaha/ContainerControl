# Modules

One host, one PostgreSQL database, one schema per module. Modules do not write each other's tables.

| Module | Schema | This slice |
| --- | --- | --- |
| Access | `access` | Identity users, teams, permission roles, permission catalog, API token placeholder, break-glass placeholder, append-only audit. Login, logout, user create/disable. |
| Platform | `platform` | Schema placeholder and `GET /platform/hosts`, which requires `platform.hosts.manage` and returns an empty list. |
| Registries | `registries` | Schema placeholder. |
| Applications | `applications` | Schema placeholder. |
| Delivery | `delivery` | Schema placeholder. |
| Edge | `edge` | Schema placeholder. |
| Runtime | `runtime` | Schema placeholder. |

The shared kernel is the clock, current user, correlation id, audit sink, permission requirement, and problem details.
