> **Historical. Not normative.** These are early planning notes. Shipped behavior is the [README](README.md), the [ADRs](docs/adr), and [production setup](docs/production-setup.md). Do not treat this file as the implementation spec.

# ContainerControl — Internal Platform Notes

Working notes from planning a self-service container hosting platform on Windows Server, replacing per-app VMs.

## Background

- Company runs internal apps on individually provisioned VMs, shared directly with dev teams.
- Stack mix: .NET, Python, Angular, React, plus databases.
- Only Windows server expertise is available in-house.
- Goal: stop giving developers direct VM access; centralize deployment through a control platform (own build, code name **ContainerControl**, since Plesk/cPanel-style tools are costly and Linux-first).

## Architecture

- **App tier**: Linux VM(s) running under Hyper-V on the Windows Server host, running Docker Engine. Most of the stack (.NET Core/5+, Python, Angular/React builds, most DBs) runs fine as Linux containers.
- **Windows containers**: reserved only for legacy .NET Framework apps that need the full Windows/IIS stack — kept as a separate, smaller host rather than the default.
- **Data tier**: separate VM(s), admin-provisioned, no direct developer access. Devs get connection strings/secrets, not server access.
- **Reverse proxy**: Traefik or nginx in front of the app tier for hostname routing, SSL termination, and label-based service discovery.
- WSL2 is treated as a dev tool, not a production host — production Docker runs on a proper Linux VM guest.

## Registries

**Decision: support both cloud and self-hosted registries, admin chooses per app.**

- Cloud: Azure Container Registry (ACR), Amazon ECR, DockerHub — admin connects centrally.
    - ECR tokens expire every 12 hours — needs a background refresh job if ContainerControl pulls on devs' behalf.
    - ACR access via scoped service principal (`AcrPull` on specific repos), not shared admin credentials.
- Self-hosted (for sensitive apps or to avoid external dependency): **Harbor** (CNCF project — adds RBAC, vulnerability scanning, UI, closest to ACR/ECR feature-wise) or plain **`docker/distribution`** (registry:2 — minimal, no UI).
- ContainerControl needs a "registry type" field per app, not a single hardcoded provider — devs/admin pick cloud or self-hosted per application.
- Registry credentials live in the secrets manager, not in ContainerControl's own database.

## Tooling constraint

**No commercial services or licensed libraries anywhere in the stack** — every component below is open source and self-hosted. This ruled out Azure Key Vault and Entra ID specifically, and steers CI/CD toward self-hosted options: **Jenkins**, **GitLab CE**, or **Woodpecker CI**, rather than Azure DevOps/GitHub Actions' hosted tiers. Docker Engine and the Compose CLI (Apache-2.0) are fine to use directly on the Linux VM — just avoid *Docker Desktop*, which carries commercial licensing terms for larger companies.

## Deployment model

- Devs keep their app Docker-compatible; CI/CD (with DevOps help) builds and pushes images to an allowed registry.
- Devs configure their app in ContainerControl: image/registry, port, hostname, and optionally a full `docker-compose.yml` for multi-container apps (app + cache + worker, etc.).
- Compose files are validated before use — block `privileged: true`, `network_mode: host`, arbitrary host bind-mounts; allow named volumes.
- Only explicitly marked services get external exposure; everything else stays on the internal Docker network by default.
- **Open decision**: whether to allow database images inside compose files at all, or require prod databases to come from the dedicated data tier (recommended, to preserve the app/data separation).

## Secrets management

- A dedicated secrets manager is required — no plaintext credentials in compose files or ContainerControl's own DB.
- **No commercial services or licensed products** — open-source, self-hosted only. Candidates: **Infisical** (Apache-2.0, self-hosted, friendlier UI, project/environment-scoped secrets) or **OpenBao** (Linux Foundation's fully open-source fork of HashiCorp Vault, created after Vault itself moved to the non-open BSL license — same capability, same API shape, but stays open source).
- Pattern: devs reference a secret by name/path in ContainerControl; the backend (using its own scoped service identity) fetches the value at deploy time and injects it directly into the container — never stored or logged by ContainerControl, never shown back in the UI.
- Support both **env var** injection (simple, default) and **file-mount** injection (more secure, e.g. Docker secrets style at `/run/secrets/`) — required for the most sensitive credentials.
- Secrets should be environment-scoped (dev/staging/prod), so a dev environment can't accidentally resolve a prod secret.

## SSL & DNS

- Default: **Let's Encrypt** via Traefik ACME — fully automatable.
- Commercial certs (e.g. GoDaddy SSL): not automatable the same way — needs a manual CSR/upload path in ContainerControl as an exception flow.
- DNS provider API integration for automated record management.

## ContainerControl — feature set

### Admin

- User/team management, RBAC, SSO (**Keycloak** — open source, self-hosted IAM; no commercial identity service)
- Application provisioning with resource quotas
- Registry connection management (ACR/ECR/DockerHub)
- Docker host management (add/remove Linux VM hosts)
- Global SSL/DNS configuration
- Platform-wide audit log and resource/capacity dashboard
- Optional prod-deploy approval gate
- Break-glass direct access for incident response

### Developers

- Point app to an image/registry, or submit a compose file
- Configure env vars/secrets (masked after entry), ports, hostname
- Trigger Let's Encrypt SSL for their hostname; request commercial cert as an exception
- Deploy/redeploy, rollback, start/stop/restart their own app
- Real-time and historical logs, per-service status and resource usage
- Deployment history
- API token/webhook for CI/CD-triggered deploys

### Deferred to later phases

- Auto-scaling / multi-replica load balancing
- Blue/green or canary deployments
- Built-in alerting (Slack/email on crash/failure)
- Cost/usage dashboards
- One-click template marketplace (databases, Redis, etc.)

## Build approach note

Building ContainerControl fully from scratch (container lifecycle, RBAC, compose orchestration, reverse proxy + cert automation, log streaming) is a significant undertaking. Worth deciding explicitly between:

1. **Full custom build** — most control, most effort.
2. **Thin custom UI over Portainer's API** — keeps your own workflow/branding/approval process, while inheriting Portainer's hardened container-lifecycle, RBAC, and Compose handling. Likely the faster path given limited in-house Linux/DevOps depth.

Either way: talk to the Docker Engine REST API/SDK (e.g. `Docker.DotNet`, `docker-py`), never shell out to the `docker` CLI from the backend, and never expose the raw Docker socket over the network without TLS + mutual auth.
