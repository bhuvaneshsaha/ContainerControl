# Modules

One host, one PostgreSQL database, one schema per module. Modules do not write each other's tables.

| Module | Schema | This slice |
| --- | --- | --- |
| Access | `access` | Identity users, teams, permission roles, permission catalog, API token issuance, an unevaluated break-glass table, and append-only audit. Login, logout, and user create/disable. There is no audit read endpoint yet. |
| Platform | `platform` | Docker host registration and Engine version ping. `GET /platform/hosts/choices` returns names for app placement. Quotas and capacity are not stored yet. |
| Registries | `registries` | Schema placeholder. Image pulls use the Engine's default registry access. |
| Applications | `applications` | Desired state and secret references. Values are written to Infisical. A `prod` application is marked as requiring approval. |
| Delivery | `delivery` | Compose policy, deploy, rollback, worker lease, and the CI webhook. Dependencies start first, and a service healthcheck is waited on before the next service. `POST /apps/{id}/approve` runs a deploy that was parked for approval. A failed Engine call is stored on the deployment as a short message that does not include secret values. |
| Edge | `edge` | Allowed domains, the `edge` network, and the Traefik container. A prepare failure returns 502. |
| Runtime | `runtime` | One-shot log text and container CPU and memory stats from the Engine. There is no live tail. |

Each application gets a private Docker network named `cc-app-{id}` with a `/24`. That keeps a host from exhausting Docker's default address pools. Exposed services also join `edge`.

The shared kernel is the clock, current user, correlation id, audit sink, permission requirement, and problem details.
