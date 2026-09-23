# ContainerControl

Self-service on-premises container control plane. Developers never receive VM or Docker socket access. This repository is the control plane: one ASP.NET Core host, one PostgreSQL database, and an online-only Angular app.

This slice signs users in with an HTTP-only cookie, stores permission roles, and shows the signed-in user's permission codes. Docker Engine, Infisical, Traefik, and deploys are later slices. Decisions are recorded under [docs/adr](docs/adr).

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
| `Cors:AllowedOrigins` | SPA origin. Default `http://localhost:4200` (config key, not an environment variable name of its own; override with `Cors__AllowedOrigins__0`) |

There is no self-registration endpoint.

## What this slice contains

- Access: Identity cookie sign-in and sign-out, admin-provisioned users, teams, permission roles, the permission catalog, an API token table placeholder, a break-glass table placeholder, and append-only audit.
- Platform, Registries, Applications, Delivery, Edge, and Runtime: schema placeholders. `GET /platform/hosts` requires `platform.hosts.manage` and returns an empty list.
- Angular: sign-in form, a plain shell, and the permissions page. Nav and route guards use `hasPermission`.

Permission codes are listed in [docs/permissions.md](docs/permissions.md). Module boundaries are in [docs/modules.md](docs/modules.md). Logs, traces, and health checks are in [docs/observability.md](docs/observability.md). The shared UI catalog is in [docs/components/README.md](docs/components/README.md).

## Production

[docs/production-setup.md](docs/production-setup.md) lists the manual steps still ahead. Hyper-V, DNS, firewall rules, Docker Engine install, and the Infisical machine identity are not automated.

## Tests

```bash
dotnet test tests/ContainerControl.Host.IntegrationTests/ContainerControl.Host.IntegrationTests.csproj
npm test --prefix client -- --watch=false
```

Integration tests start PostgreSQL with Testcontainers and cover health, sign-in, a rejected unknown user, and permission denial on `GET /platform/hosts`.
