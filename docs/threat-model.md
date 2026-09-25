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

Application hostnames and allowed domains are stored only as DNS names. A pasted URL is reduced to its host. Backticks, parentheses, and other Traefik rule characters are rejected on save and again when the `Host()` label is built, so a name cannot add a second matcher.

Secret and registry values are written to Infisical. A failed deploy is stored as a short message. Messages that look like an assignment are replaced with a generic sentence. ECR refresh logs the exception type and the registry id.

Live logs use SignalR at `/hubs/logs`. The hub requires `runtime.logs.read` and team membership. The stream is the Engine log API, not a separate log store.

## Operator work the product cannot do

These stay outside the API. [Production setup](production-setup.md) is the checklist.

- Create the Hyper-V VMs, install Docker Engine, and keep mutual TLS on the Engine. Do not publish the raw socket.
- Forward ports 80 and 443 to Traefik and create DNS records at the provider. The product does not write DNS.
- Create the Infisical machine identities and keep each environment's credential on the management host.
- Create the registry user or access key in ACR, ECR, Docker Hub, or Harbor before saving the connection.
- Provision data-tier databases. Compose will not run those images.
- Buy a commercial certificate when Let's Encrypt cannot issue one. Traefik's ACME resolver covers public hostnames only.
- Rotate the Docker host client certificate on the host, then update the Infisical reference.

## Deferred

Entra ID, Windows container hosts, per-app DNS writes, a long-term log store, auto-scaling, blue/green, canary, alerting, cost dashboards, and a template marketplace are not implemented. The control plane is one management VM. A second Docker host does not receive public traffic by itself.
