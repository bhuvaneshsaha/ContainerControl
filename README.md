# ContainerControl

Self-service on-premises container control plane. Developers never receive VM or Docker socket access. This repository is the control plane: one ASP.NET Core host, one PostgreSQL database, and an online-only Angular app.

Sign-in, permission checks, Docker host registration, secret references, deploys, Traefik labels, logs, and the CI webhook are in place. Decisions are recorded under [docs/adr](docs/adr).

## Local setup

Prerequisites: .NET 10 SDK, Node.js 22 LTS, and Docker Engine. The setup scripts check those versions, start PostgreSQL from `deploy/local/compose.yaml`, and print the API and SPA commands. Infisical, Traefik, and the OpenTelemetry collector stay behind Compose profiles and are not started.

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

Infisical and Traefik stay behind Compose profiles until you ask for them. The API can also create the `edge` network and the Traefik container when an admin prepares a Docker host. Do not start the Traefik profile and prepare the same host at the same time; both want ports 80 and 443 and the name `cc-traefik`.

```bash
./scripts/dev-setup.sh --profile infisical
./scripts/dev-setup.sh --profile traefik
```

The Infisical profile uses local sample credentials in `deploy/local/compose.yaml`. They are not production secrets. Creating the machine identity is still a manual step. Set the environment variables below before saving a secret.

### Development sample users

Created only when `ASPNETCORE_ENVIRONMENT` is `Development`. Do not use these accounts in production.

| Email | Password | Role |
| --- | --- | --- |
| `admin@localhost` | `Dev-Admin-Passw0rd!` | Platform administrator (every catalog code) |
| `developer@localhost` | `Dev-Developer-Passw0rd!` | Developer (no `platform.hosts.manage`) |

Sign in at `http://localhost:4200/sign-in`. The Permissions page lists codes from `GET /me/permissions`. After a role change, sign in again so the cookie picks up the new codes.

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
| `EDGE_ACME_EMAIL` | Optional Let's Encrypt account email |
| `EDGE_HTTP_PORT` | Host port published for Traefik HTTP. Default `80` |
| `EDGE_HTTPS_PORT` | Host port published for Traefik HTTPS. Default `443` |

There is no self-registration endpoint.

## What this slice contains

- Access: Identity cookie sign-in and sign-out, admin-provisioned users, teams, permission roles, the permission catalog, API token issuance, a break-glass table that is not evaluated, and append-only audit. The API does not yet list audit rows.
- Platform: Docker host registration and an Engine version ping. Preparing a host creates the `edge` network and the Traefik container. Quotas and capacity are not stored.
- Applications: desired state and secret references. Secret values are written to Infisical and are not stored in PostgreSQL. Deploy places them in the container as environment variables, or as files under `/run/secrets`, and fills `${SECRET}` placeholders in the compose environment and command. Creating a `prod` application sets approval required.
- Delivery: compose policy, deploy, start, stop, restart, rollback, and the CI webhook. `POST /apps/{id}/deploy` on an approval-required application stays `pending-approval` until `POST /apps/{id}/approve`. A failed deploy is recorded with a short message. That message omits text that looks like a secret assignment. A compose healthcheck is applied on the container, and deploy waits for healthy before starting the services that depend on it.
- Edge: allowed domains and Traefik labels for an exposed hostname. If prepare cannot reach the Engine, the API returns 502.
- Runtime: a one-shot log read, a SignalR tail at `/hubs/logs` (`Tail` sends `log` events from the Engine log API), and container CPU and memory stats. The tail requires `runtime.logs.read` and team membership.
- Registries: schema placeholder. Image pulls use the Engine's existing registry credentials.
- Angular: sign-in, permissions, applications, secrets, hosts, domains, users and teams, and tokens. Nav and route guards use permission codes. The Applications page has no Approve action. The Users page assigns an existing role; it does not edit the catalog on a role, and it does not show audit.

Permission codes are listed in [docs/permissions.md](docs/permissions.md). Module boundaries are in [docs/modules.md](docs/modules.md). Logs, traces, and health checks are in [docs/observability.md](docs/observability.md). The shared UI catalog is in [docs/components/README.md](docs/components/README.md).

## Production

[docs/production-setup.md](docs/production-setup.md) lists the manual steps still ahead. Hyper-V, DNS, firewall rules, Docker Engine install, and the Infisical machine identity are not automated.

## Tests

```bash
dotnet test tests/ContainerControl.Host.IntegrationTests/ContainerControl.Host.IntegrationTests.csproj
npm test --prefix client -- --watch=false
```

Integration tests start PostgreSQL with Testcontainers. They cover health, sign-in, a rejected unknown user, permission denial, Engine version ping, secret storage, deploy, app-network isolation, compose rejection, Traefik routing, logs, stats, rollback, and the deploy webhook. The secret check talks to an in-process stand-in of the Infisical v4 API. Creating a real machine identity is still manual.
