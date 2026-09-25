# Production setup

This is the operator checklist for work ContainerControl cannot do, followed by the steps you finish in the product. Local scripts start PostgreSQL and print the API and SPA commands. They are not the production install.

Do not run `ASPNETCORE_ENVIRONMENT=Development` in production. That seed creates sample users. Production gets the permission catalog and, when the database has no users, one administrator from the host environment variables.

The [threat model](threat-model.md) records what the API enforces. Confirm each check below before the next step.

## Steps the product cannot do

1. Create the Hyper-V Linux management VM and the separate Linux Docker host. Check: both VMs boot and the management VM is not a tenant app host.
2. Assign the static public IP and forward ports 80 and 443 to the Docker host. Check: a request to that IP reaches the host. Keep those ports forwarded only to Traefik once that container exists.
3. At the DNS provider, point the domain and its wildcard at that IP. A new apex domain is also created at the provider. ContainerControl does not write DNS records. Check: the wildcard resolves to the public IP before you add the domain in the product.
4. Install Docker Engine on the Docker host. Do not install Docker Desktop and do not publish the raw socket. A `tcp://` endpoint outside Development needs the client certificate, client key, and CA from Infisical or from path references on the host row. PostgreSQL stores those references and does not store PEM. Cleartext `tcp://` is rejected. `unix` and `npipe` do not need that material. Development may keep cleartext `tcp://` for a nested or demo Engine. There is no separate lab insecure flag. See [ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md). That check is PLAT-02 and is not in the API until that story. Check: the Engine answers on the management VLAN.
5. Start ContainerControl and PostgreSQL on the management VM with the production connection string in `ConnectionStrings__Database`. Check: `/health/ready` reports the database healthy.
6. Set `CONTAINERCONTROL_ADMIN_EMAIL`, `CONTAINERCONTROL_ADMIN_PASSWORD`, and optionally `CONTAINERCONTROL_ADMIN_DISPLAY_NAME` before the first boot if the database has no users. Check: that account can sign in. The password is not stored in git.
7. Create a separate Infisical machine identity for dev, staging, and prod. Put `INFISICAL_SITE_URL` and each environment's `INFISICAL_*_CLIENT_ID`, `INFISICAL_*_CLIENT_SECRET`, and `INFISICAL_*_PROJECT_ID` on the management host. Check: those variables are present on the host and absent from git. ContainerControl writes secret values through the Infisical API. It does not create the identity.
8. Create the registry credential in ACR, ECR, Docker Hub, or Harbor. `registry:2` is not a connection type. Check: the registry accepts that credential. Saving it in ContainerControl is the next section.
9. Provision a data-tier database on its own VM. Check: the database accepts connections from the Docker host. Add the connection string later as a secret. Compose database images are rejected.
10. When Let's Encrypt cannot issue a certificate, obtain one from the vendor and install it yourself. Traefik's ACME resolver, controlled by `EDGE_ACME_EMAIL` (`Edge:AcmeEmail`), covers public hostnames only. When that email is set, prepare emits `websecure`, `tls=true`, and `tls.certresolver=le` on the same validated Host() router, and HTTP redirects to HTTPS. With the email unset, routers stay on `web` only ([ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md), PLAT-01). The product does not buy or install a commercial certificate. This wave does not implement `edge.certs.manage`.
11. When the Docker host client material must change, rotate the client certificate, key, and CA, then update the Infisical or path references on the host row. The product does not store PEM and does not rotate that material.

## Nested Docker host storage driver

If the Docker host's data root is already an overlay filesystem — a nested VM, or Engine running inside another container — the kernel `overlay2` driver cannot mount another overlay on top. The host operator sets the daemon storage driver to `fuse-overlayfs` and restarts Docker. ContainerControl does not choose or configure that driver. A deploy's storage limit is quota accounting only. It is not sent as a Docker storage option.

## Steps you finish in ContainerControl

1. Sign in and create real users. Assign roles built from the permission catalog. Check: a developer cannot call `GET /platform/hosts`.
2. Register the Docker host and ping the Engine. Prepare the host so the `edge` network and the Traefik container exist, or start the Traefik Compose profile on that host. Do not do both against the same ports and container name. When `EDGE_ACME_EMAIL` (`Edge:AcmeEmail`) is set, deploy stamps `websecure`, `tls=true`, and `tls.certresolver=le` on the same validated Host() router, and prepare redirects HTTP to HTTPS when it creates Traefik. With ACME unset, the HTTP `web` entrypoint is enough, including a localhost demo. See [ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md). A Traefik container that already exists is left as it is. Delete `cc-traefik` and prepare again to pick up a new email. If prepare cannot reach the Engine, the API returns 502 and does not claim the container was created.
3. Add allowed domains that already resolve to the public IP.
4. Save the registry connection. The password or ECR keys are written to Infisical. PostgreSQL stores the path. An ECR token refreshes before it expires.
5. On the Capacity page, save each team's CPU, memory, and storage quota, then read host capacity from the Engine. A deploy is rejected when a service omits those limits or the sum exceeds the quota.
6. Use break-glass only when a catalog permission is needed for a short time. `POST /access/break-glass` requires `access.breakglass.grant`, lasts 5 to 60 minutes, and writes an audit row. It does not open a shell or the Docker socket.
7. Deploy one sample app on a hostname under an allowed domain. Check: the exposed hostname returns the app, and an unexposed service does not. Optional `EDGE_HTTP_PORT` and `EDGE_HTTPS_PORT` control the Traefik container created by prepare.

## Do

- Keep tenant containers on a private app network, with only the exposed service on the edge network.
- Create every user explicitly.
- Store secret values only through the ContainerControl secret form. PostgreSQL keeps the name, path, injection mode, and the compose services that receive the secret. After upgrading to service targets, open each secret and save it again with those services. Existing rows are injected nowhere until you do. An empty assignment does not mean every service.
- Keep production databases on the data-tier VM.
- Deploy, restart, and read logs from ContainerControl.

## Do not

- Do not install Docker Desktop on the server or publish the Docker socket.
- Do not publish the raw Docker socket to tenant containers. The compose denylist stays enforced. Traefik v1 keeps the Docker provider and the host socket mounted read-only. A socket proxy and a file provider are not part of v1. Traefik holding that socket is an accepted high-value risk until they land. See [ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md).
- Do not give developers a VM login, a Docker socket, or the Infisical machine credential.
- Do not put secret values in git, compose files, or PostgreSQL.
- Do not turn on self-registration or run the Development user seed in production.
- Do not add Portainer, Keycloak, or a commercial control-panel product.
- Do not expect Entra ID, Windows container hosts, per-app DNS writes, a long-term log store, auto-scaling, blue/green, canary, alerting, cost dashboards, or a template marketplace. Those are not in this product.
