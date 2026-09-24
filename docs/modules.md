# Modules

One host, one PostgreSQL database, one schema per module. Modules do not write each other's tables.

| Module | Schema | This slice |
| --- | --- | --- |
| Access | `access` | Identity users, teams, permission roles composed from the catalog, API token issuance, break-glass grants of 5 to 60 minutes, and append-only audit. An unexpired grant is included by the permission check and by `/auth/session`. It does not open a shell or the Docker socket. `GET /access/audit` reads the latest rows for `access.audit.read`. |
| Platform | `platform` | Docker host registration, Engine version ping, team CPU/memory/storage quotas, and a host capacity table. `GET /platform/hosts/choices` returns names for app placement and omits the Engine endpoint. |
| Registries | `registries` | Connections typed `Acr`, `Ecr`, `DockerHub`, or `Harbor`. Pull credentials are Infisical paths. ECR tokens refresh in the background. |
| Applications | `applications` | Desired state and secret references. Values are written to Infisical. `prod` always requires approval. Other environments require it when the application opts in. |
| Delivery | `delivery` | Compose policy, deploy, rollback, worker lease, and the CI webhook. Dependencies start first, and a service healthcheck is waited on before the next service. A team quota rejects a deploy that is missing CPU, memory, or storage limits or that exceeds the allowance. `POST /apps/{id}/approve` runs a deploy that was parked for approval. A failed Engine call is stored on the deployment as a short message that does not include secret values. |
| Edge | `edge` | Allowed domains, the `edge` network, and the Traefik container. A prepare failure returns 502. |
| Runtime | `runtime` | One-shot log text, a SignalR tail from the Engine log API at `/hubs/logs`, and container CPU and memory stats. |

Each application gets a private Docker network named `cc-app-{id}` with a `/24`. That keeps a host from exhausting Docker's default address pools. Exposed services also join `edge`.

The shared kernel is the clock, current user, correlation id, audit sink, permission requirement, and problem details.
