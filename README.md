# ContainerControl

Self-service on-premises container control plane. Developers never receive VM or Docker socket access. This repository is the control plane: one ASP.NET Core host, one PostgreSQL database, and an online-only Angular app.

Sign-in, permission checks, Docker host registration, secret references, deploys, Traefik labels, logs, and the CI webhook are in place. Decisions are recorded under [docs/adr](docs/adr).

## Local setup

Prerequisites: .NET 10 SDK, Node.js 22 LTS (`.nvmrc`; the client `package.json` `engines` field asks for the same major), and Docker Engine. The setup scripts check those versions, start PostgreSQL from `deploy/local/compose.yaml`, and print the API and SPA commands. Infisical, Traefik, and the OpenTelemetry collector stay behind Compose profiles and are not started. If the Docker host is nested and `overlay2` cannot start, see [Nested Docker host storage driver](docs/production-setup.md#nested-docker-host-storage-driver). The product does not choose the storage driver.

```bash
./scripts/dev-setup.sh
```

Windows:

```bat
scripts\dev-setup.bat
```

Then, from the repository root:

```bash
dotnet run --project src/Host/ContainerControl.Host.csproj
npm start --prefix client
```

The API listens on `http://localhost:5080`. The SPA listens on `http://localhost:4200` and calls the API with cookies (`withCredentials`). Startup applies EF Core migrations. OpenAPI is at `http://localhost:5080/openapi/v1.json` when the host is not running in Production.

The Development connection string is in `src/Host/appsettings.Development.json`. It matches the Compose PostgreSQL user and database. That password is local sample configuration, not a production secret.

Infisical, Traefik, and the OpenTelemetry collector stay behind Compose profiles until you ask for them. The API can also create the `edge` network and the Traefik container when an admin prepares a Docker host. Do not start the Traefik profile and prepare the same host at the same time; both want ports 80 and 443 and the name `cc-traefik`.

```bash
./scripts/dev-setup.sh --profile infisical
./scripts/dev-setup.sh --profile traefik
./scripts/dev-setup.sh --profile observability
```

The `observability` profile starts the local OTLP collector on `4317` and `4318`. Details are in [docs/observability.md](docs/observability.md). The Infisical profile uses local sample credentials in `deploy/local/compose.yaml`. They are not production secrets. Creating the machine identity is still a manual step. Set the environment variables below before saving a secret.

When `INFISICAL_SITE_URL` and that environment's client id, client secret, and project id are set, secret values go to Infisical. In Development only, if those credentials are absent, the API writes the value under `deploy/local/secret-store/` instead. That path is gitignored. It is not used outside Development: any other environment fails the write until Infisical is configured. PostgreSQL still stores the path, environment, injection mode, and service targets, and the API does not return the value. Secrets saved before service targets existed have no targets until an operator assigns services. See [ADR 0011](docs/adr/0011-secret-service-targets.md).

### Development sample users

Created only when `ASPNETCORE_ENVIRONMENT` is `Development`. Do not use these accounts in production.

| Email | Password | Role |
| --- | --- | --- |
| `admin@localhost` | `Dev-Admin-Passw0rd!` | Platform administrator (every catalog code) |
| `developer@localhost` | `Dev-Developer-Passw0rd!` | Developer (no `platform.hosts.manage`) |

Sign in at `http://localhost:4200/sign-in`. After sign-in the app opens Applications when the account has `apps.read`, otherwise the first page that account can open. My access (`/permissions`) lists the same codes from `GET /me/permissions` with catalog names. A link to a page the account cannot open returns to that home and names the missing permission. After a role change, sign in again so the cookie picks up the new codes.

### First production administrator

When the database has no users, startup creates one administrator from host environment variables. Values are never committed.

| Variable | Purpose |
| --- | --- |
| `CONTAINERCONTROL_ADMIN_EMAIL` | Administrator email |
| `CONTAINERCONTROL_ADMIN_PASSWORD` | Administrator password |
| `CONTAINERCONTROL_ADMIN_DISPLAY_NAME` | Optional display name. Default is Administrator |
| `ConnectionStrings__Database` | PostgreSQL connection string |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP endpoint. Default `http://localhost:4317` |
| `Cors__AllowedOrigins__0` | SPA origin. Default `http://localhost:4200` |
| `INFISICAL_SITE_URL` | Infisical site URL |
| `INFISICAL_DEV_CLIENT_ID` | Dev machine-identity client id |
| `INFISICAL_DEV_CLIENT_SECRET` | Dev machine-identity client secret |
| `INFISICAL_DEV_PROJECT_ID` | Dev Infisical project id |
| `INFISICAL_STAGING_CLIENT_ID` | Staging machine-identity client id |
| `INFISICAL_STAGING_CLIENT_SECRET` | Staging machine-identity client secret |
| `INFISICAL_STAGING_PROJECT_ID` | Staging Infisical project id |
| `INFISICAL_PROD_CLIENT_ID` | Prod machine-identity client id |
| `INFISICAL_PROD_CLIENT_SECRET` | Prod machine-identity client secret |
| `INFISICAL_PROD_PROJECT_ID` | Prod Infisical project id |
| `EDGE_ACME_EMAIL` | Optional Let's Encrypt account email. When set, exposed routers use `websecure`, `tls=true`, and `tls.certresolver=le`, and prepare redirects HTTP to HTTPS |
| `EDGE_HTTP_PORT` | Host port published for Traefik HTTP. Default `80` |
| `EDGE_HTTPS_PORT` | Host port published for Traefik HTTPS. Default `443` |

There is no self-registration endpoint.

## What this slice contains

- Access: Identity cookie sign-in and sign-out, admin-provisioned users, teams, permission roles, the permission catalog, API token issuance, short-lived break-glass grants, and append-only audit. `POST /access/break-glass` with `access.breakglass.grant` adds one catalog permission for 5 to 60 minutes. The permission API and `/auth/session` include an unexpired grant. The cookie keeps assigned role permissions, so a grant expires without a new sign-in. A grant does not open a shell or the Docker socket. `GET /access/audit` lists the latest rows for `access.audit.read` and does not include secret values. The Access page creates and disables users, creates teams, edits roles from catalog checkboxes, grants break-glass access, and reads that audit.
- Platform: Docker host registration and an Engine version ping. A `tcp://` endpoint outside Development requires `file:` or `infisical:` references for the client certificate, key, and CA. PostgreSQL stores the references, not PEM. Development may register cleartext `tcp://`. Preparing a host creates the `edge` network and the Traefik container. `PUT /platform/quotas/{teamId}` stores that team's CPU, memory, and storage quota. Deploy rejects a plan that omits those limits or exceeds the quota. `GET /platform/capacity` lists host CPU, memory, and storage. `POST /platform/capacity/{hostId}` reads them from the Engine. The Engine endpoint is not in that response.
- Applications: desired state and secret references. Secret values are written to Infisical when that environment's machine credentials are set, or to the Development file fallback above when they are not. They are not stored in PostgreSQL. Each secret is assigned to compose service names. Deploy places it only in those containers, as an environment variable or a file under `/run/secrets`, and fills `${SECRET}` placeholders in that service's environment and command. A service that references an unassigned secret fails the deploy. Creating a `prod` application sets approval required. Any environment can opt in with the same flag.
- Delivery: compose policy, deploy, start, stop, restart, rollback, and the CI webhook. A deploy holds a lease for that application and environment only. Another deploy of the same pair returns 409 Conflict and does not wait; the lease expires after five minutes if it is not released. Other applications, and other environments of the same application id, deploy at the same time. See [ADR 0013](docs/adr/0013-deploy-lease-per-application-environment.md). `POST /apps/{id}/deploy` on an approval-required application stays `pending-approval` until a caller with `deploy.approve` calls `POST /apps/{id}/approve`. That caller does not have to be a member of the application's team. The Applications page shows Approve for a pending application when the signed-in user has that permission. A failed deploy is recorded with a short message. That message omits text that looks like a secret assignment. A compose healthcheck is applied on the container, and deploy waits for healthy before starting the services that depend on it.
- Edge: allowed domains and Traefik labels for an exposed hostname. A compose service may set `x-containercontrol.hostname`; an exposed service that does not uses the application hostname. See [ADR 0014](docs/adr/0014-per-service-public-hostnames.md). When `EDGE_ACME_EMAIL` is set, those labels use `websecure` with the `le` resolver and prepare redirects HTTP to HTTPS. Otherwise they stay on `web`. See [ADR 0016](docs/adr/0016-traefik-websecure-engine-mtls-and-socket.md). If prepare cannot reach the Engine, the API returns 502.
- Runtime: a one-shot log read, a SignalR tail at `/hubs/logs` (`Tail` sends `log` events from the Engine log API), and container CPU and memory stats. The tail requires `runtime.logs.read` and team membership.
- Registries: connections typed `Acr`, `Ecr`, `DockerHub`, or `Harbor`. Passwords and ECR keys are written to Infisical. PostgreSQL stores the path. Image pulls use the matching connection. An ECR token is refreshed before the 12-hour expiry. `registry:2` is not a connection type.
- Angular: sign-in, permissions, applications, secrets, hosts, capacity, domains, access, and tokens. Nav and route guards use permission codes. The Access page is available to `access.users.manage`, `access.teams.manage`, `access.roles.manage`, `access.audit.read`, or `access.breakglass.grant`. The Capacity page is available to `platform.quotas.manage` or `platform.capacity.read`.

Permission codes are listed in [docs/permissions.md](docs/permissions.md). Module boundaries are in [docs/modules.md](docs/modules.md). Logs, traces, and health checks are in [docs/observability.md](docs/observability.md). The shared UI catalog is in [docs/components/README.md](docs/components/README.md).

## Production

[docs/production-setup.md](docs/production-setup.md) lists the manual steps the product still cannot do, then the steps you finish in the app. [docs/threat-model.md](docs/threat-model.md) records the boundaries the API enforces. Hyper-V, DNS writes, firewall rules, Docker Engine install, the Infisical machine identity, registry credential creation, data-tier databases, commercial certificates, and Docker client-certificate rotation stay manual.

## Tests

```bash
dotnet test tests/ContainerControl.Host.IntegrationTests/ContainerControl.Host.IntegrationTests.csproj
npm test --prefix client -- --watch=false
```

Integration tests start PostgreSQL with Testcontainers. They cover health, sign-in, a rejected unknown user, permission denial, Engine version ping, secret storage, deploy, app-network isolation, compose rejection, Traefik routing, logs, stats, rollback, and the deploy webhook. The secret check talks to an in-process stand-in of the Infisical v4 API. Creating a real machine identity is still manual.
