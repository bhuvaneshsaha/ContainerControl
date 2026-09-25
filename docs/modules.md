# Modules

One host, one PostgreSQL database, one schema per module. Modules do not write each other's tables.

| Module | Schema | This slice |
| --- | --- | --- |
| Access | `access` | Identity users, teams, permission roles composed from the catalog, API token issuance, break-glass grants of 5 to 60 minutes, and append-only audit. An unexpired grant is included by the permission check and by `/auth/session`. It does not open a shell or the Docker socket. `GET /access/audit` reads the latest rows for `access.audit.read`. |
| Platform | `platform` | Docker host registration, Engine version ping, team CPU/memory/storage quotas, and a host capacity table. An Engine endpoint must use the `unix`, `npipe`, or `tcp` scheme. `GET /platform/hosts/choices` returns names for app placement and omits the Engine endpoint. |
| Registries | `registries` | Connections typed `Acr`, `Ecr`, `DockerHub`, or `Harbor`. Pull credentials are Infisical paths. ECR tokens refresh in the background. |
| Applications | `applications` | Desired state and secret references. Values are written to Infisical. Each secret names the compose services that receive it; an empty list injects it nowhere. `prod` always requires approval. Other environments require it when the application opts in. |
| Delivery | `delivery` | Compose policy, deploy, rollback, a deploy lease per application and environment, and the CI webhook. A second deploy of the same application and environment returns 409 without waiting; the lease expires after five minutes if the holder does not release it. Different applications, or different environments of the same application id, deploy at the same time. Deploy injects a secret only into the services named on that secret, and fails when a service references a secret that is not assigned to it. Dependencies start first, and a service healthcheck is waited on before the next service. A team quota rejects a deploy that is missing CPU, memory, or storage limits or that exceeds the allowance. `POST /apps/{id}/approve` runs a deploy that was parked for approval. A failed Engine call is stored on the deployment as a short message that does not include secret values. An exposed service may set `x-containercontrol.hostname`; otherwise it uses the application hostname. Duplicate public hostnames in that file are rejected. See [ADR 0013](adr/0013-deploy-lease-per-application-environment.md) and [ADR 0014](adr/0014-per-service-public-hostnames.md). |
| Edge | `edge` | Allowed domains, the `edge` network, and the Traefik container. A prepare failure returns 502. |
| Runtime | `runtime` | One-shot log text, a SignalR tail from the Engine log API at `/hubs/logs`, and container CPU and memory stats. |

Each application gets a private Docker network named `cc-app-{id}` with a `/24`. That keeps a host from exhausting Docker's default address pools. Exposed services also join `edge`.

The shared kernel is the clock, current user, correlation id, audit sink, permission requirement, and problem details.
