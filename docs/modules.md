# Modules

One host, one PostgreSQL database, one schema per module. Modules do not write each other's tables.

| Module | Schema | This slice |
| --- | --- | --- |
| Access | `access` | Identity users, teams, permission roles, permission catalog, API token issuance, break-glass placeholder, append-only audit. Login, logout, and user create/disable. |
| Platform | `platform` | Docker host registration and Engine version ping. `GET /platform/hosts/choices` returns names for app placement. |
| Registries | `registries` | Schema placeholder. Image pulls use the Engine's default registry access. |
| Applications | `applications` | Desired state and secret references. Values are written to Infisical. |
| Delivery | `delivery` | Compose policy, deploy, rollback, worker lease, and the CI webhook. |
| Edge | `edge` | Allowed domains, the `edge` network, and the Traefik container. |
| Runtime | `runtime` | Log tail and container stats from the Engine. |

Each application gets a private Docker network named `cc-app-{id}` with a `/24`. That keeps a host from exhausting Docker's default address pools. Exposed services also join `edge`.

The shared kernel is the clock, current user, correlation id, audit sink, permission requirement, and problem details.
