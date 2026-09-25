# Threat model

ContainerControl is an on-premises control plane. Developers configure applications in the product. They do not get a VM login, a Docker socket, or the Infisical machine credential. This note records the boundaries the code enforces and the work that stays with the operator. It is not a penetration test.

## Assets

- Application desired state, hostnames, and deployment history in PostgreSQL.
- Secret values in Infisical. PostgreSQL stores the name, path, and injection mode.
- Registry usernames, passwords, and ECR keys in Infisical. PostgreSQL stores the path and, for ECR, the token expiry.
- The Docker Engine endpoint and its client certificate reference.
- Identity passwords, API token hashes, and the audit log.
- Tenant containers and the Traefik container on the Docker host.

## Boundaries

The browser talks to the API with an Identity cookie. State-changing requests need the anti-forgery token from `GET /auth/csrf`. `POST /delivery/webhook` is the exception: it authenticates with a bearer API token. The token is stored as a SHA-256 hash and is shown once at issue. The webhook calls the same permission check as a signed-in user and requires `deploy.execute`.

Authorization checks a permission code. It does not check a role name. A break-glass grant adds one catalog permission for 5 to 60 minutes. The permission handler reads that grant on the request. The sign-in cookie keeps the role permissions only, so the grant stops working when it expires. Granting writes `access.breakglass.granted` to the audit log. The grant does not open a shell or the Docker socket.

Application routes also require team membership, except `POST /apps/{id}/approve`. That call requires `deploy.approve` and does not require membership. `prod`, and any application that opted in, stays `pending-approval` until that call.

The API reaches Docker through the Engine API client. Host choice responses omit the endpoint. Capacity responses omit it too. Prepare, ping, and capacity reads return a fixed error when the Engine cannot be reached. They do not return the Engine body. Registration and the Engine client accept only `unix`, `npipe`, and `tcp` endpoints. Any other scheme is rejected with a fixed message that does not include the supplied address. See [ADR 0012](adr/0012-engine-endpoint-scheme-allow-list.md).

Compose is checked before any Engine call. The policy rejects `privileged`, host networking, host pid or ipc, `cap_add`, devices, `build`, bind mounts, the Docker socket, and database images. Deploy starts dependencies first and waits for a compose healthcheck, capped at five minutes. When the team has a quota, each service must declare CPU, memory, and storage limits, and the sum must fit.

A deploy lease is one row per application and environment. A second deploy of that pair returns 409 Conflict instead of changing the same containers at the same time. The row expires after five minutes, so a holder that stops without releasing it does not block that pair longer than that. See [ADR 0013](adr/0013-deploy-lease-per-application-environment.md).

Application hostnames and allowed domains are stored only as DNS names. A pasted URL is reduced to its host. Backticks, parentheses, and other Traefik rule characters are rejected on save and again when the `Host()` label is built, so a name cannot add a second matcher. A compose service may set `x-containercontrol.hostname`. That value is reduced and checked with the same rules. A repeated public hostname in one compose file is rejected before deploy calls the Engine. Each name that is routed must still sit under an allowed domain. See [ADR 0014](adr/0014-per-service-public-hostnames.md).

Secret and registry values are written to Infisical. A failed deploy is stored as a short message. Messages that look like an assignment are replaced with a generic sentence. ECR refresh logs the exception type and the registry id.

Live logs use SignalR at `/hubs/logs`. The hub requires `runtime.logs.read` and team membership. The stream is the Engine log API. Stored lines in `runtime.log_lines` use the same permission and team check. Secret values known to the catalog are replaced with `[redacted]` before insert. Alert webhook URLs and recipient addresses are platform settings. The SMTP password is host configuration and is not stored in PostgreSQL. Alert bodies do not include secret values.

## Decided mitigations

These rules are accepted in [ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md). Hyper-V, DNS, and Infisical identity setup stay with the operator, in the list below.

- Public HTTPS. When `Edge:AcmeEmail` / `EDGE_ACME_EMAIL` is set, the same validated `Host()` router (`PublicHostname.TraefikHostRule`, APP-01 and APP-05) emits `websecure`, `tls=true`, and `tls.certresolver=le`. HTTP on `web` redirects to HTTPS. With ACME unset, routers stay on `web` only. The product does not write DNS. Commercial certificates stay with the operator. This wave does not implement `edge.certs.manage`.
- Engine `tcp://`. Outside Development, registration and connect require the client certificate, client key, and CA from Infisical or from path references on the host row. PostgreSQL stores those references and does not store PEM. Cleartext `tcp://` is rejected with a fixed message that does not include the address. `unix` and `npipe` stay on the local trust boundary. Development may register cleartext `tcp://` for a nested or demo Engine. v1 has no separate lab insecure flag.
- Traefik socket. Traefik v1 keeps the Docker provider and the host socket mounted read-only, as in [ADR 0004](adr/0004-one-public-ip-traefik-labels.md). The tenant compose Docker socket denylist stays enforced. A socket proxy and a Traefik file provider are deferred and are not a v1 control. That deferral does not reopen PLAT-04.

The `websecure` labels and the HTTP redirect are enforced when `EDGE_ACME_EMAIL` is set. Prepare adds that redirect only when it creates Traefik. An existing `cc-traefik` container is left as it is. The `tcp://` certificate check is enforced on registration and on connect outside Development. References are `file:` or `infisical:` paths. PEM is not stored. The read-only Traefik socket mount and the tenant socket denylist are already in place.

Accepted residual: Traefik holding the host Docker socket is a high-value v1 risk. It stands until a socket proxy or the file provider lands. It is accepted in [ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md) and is not a reason to reopen PLAT-04.

## Operator work the product cannot do

These stay outside the API. [Production setup](production-setup.md) is the checklist.

- Create the Hyper-V VMs and install Docker Engine. Do not publish the raw socket. The operator still places the client certificate, key, and CA in Infisical or on the host path references. The API stores those references and does not store PEM.
- Forward ports 80 and 443 to Traefik and create DNS records at the provider. The product does not write DNS.
- Create the Infisical machine identities and keep each environment's credential on the management host.
- Create the registry user or access key in ACR, ECR, Docker Hub, or Harbor before saving the connection.
- Provision data-tier databases. Compose will not run those images.
- Buy a commercial certificate when Let's Encrypt cannot issue one. Traefik's ACME resolver covers public hostnames only. This wave does not implement `edge.certs.manage`. The operator installs that certificate outside the API.
- Rotate the Docker host client certificate, key, and CA, then update the Infisical or path reference. The product does not store PEM.

## Deferred

Entra ID, Windows container hosts, per-app DNS writes, auto-scaling, cost dashboards, and a template marketplace are not implemented. Blue/green, canary, stored logs, and webhook or SMTP alerts are in place ([ADR 0017](adr/0017-traefik-slot-swap-and-canary.md)). A Docker socket proxy and a Traefik file provider are deferred past v1 ([ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md)). Traefik holding the host socket until then is the accepted residual in that ADR, not an open Critical. The control plane is one management VM. A second Docker host does not receive public traffic by itself. Slot routing still uses this Traefik. It does not add a second public entry.
