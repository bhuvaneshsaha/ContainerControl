# Production setup

This guide lists the manual steps startup cannot do. This slice does not automate Hyper-V, DNS, firewall rules, Docker Engine install, or the Infisical machine identity. Local scripts start PostgreSQL and print the API and SPA commands. They are not the production install.

Do not run `ASPNETCORE_ENVIRONMENT=Development` in production. That seed creates sample users. Production gets the permission catalog and, when the database has no users, one administrator from the host environment variables.

## Steps still ahead

1. Create the Hyper-V Linux management VM and the separate Linux Docker host. Check: both VMs boot and the management VM is not a tenant app host.
2. Assign the static public IP and forward ports 80 and 443 to the Docker host. Check: a request to that IP reaches the host.
3. At the DNS provider, point the domain and its wildcard at that IP. Check: the wildcard resolves to the public IP.
4. Install Docker Engine on the Docker host and leave it running with mutual TLS. Do not install Docker Desktop and do not publish the raw socket. Check: the Engine answers on the management VLAN.
5. Start ContainerControl and PostgreSQL on the management VM with the production connection string in `ConnectionStrings__Database`. Check: `/health/ready` reports the database healthy.
6. Set `CONTAINERCONTROL_ADMIN_EMAIL`, `CONTAINERCONTROL_ADMIN_PASSWORD`, and optionally `CONTAINERCONTROL_ADMIN_DISPLAY_NAME` before the first boot if the database has no users. Check: that account can sign in. The password is not stored in git.
7. Create a separate Infisical machine identity for dev, staging, and prod. Put `INFISICAL_SITE_URL` and each environment's `INFISICAL_*_CLIENT_ID`, `INFISICAL_*_CLIENT_SECRET`, and `INFISICAL_*_PROJECT_ID` on the management host. Check: those variables are present on the host and absent from git. ContainerControl writes secret values through the Infisical API. It does not create the identity.
8. Sign in and create real users. Assign roles built from the permission catalog. Check: a developer cannot call `GET /platform/hosts`.
9. Register the Docker host and ping the Engine. Prepare the host so the `edge` network and the Traefik container exist, or start the Traefik Compose profile on that host. Do not do both against the same ports and container name. If prepare cannot reach the Engine, the API returns 502 and does not claim the container was created. Add allowed domains that already resolve to the public IP. Create the registry credential in ACR, ECR, Docker Hub, or Harbor, then save the connection in ContainerControl. The password is written to Infisical. On the Capacity page, save each team's CPU, memory, and storage quota, then read host capacity from the Engine. A caller with `access.breakglass.grant` can grant one catalog permission for 5 to 60 minutes from the Access page. That grant is written to the audit log and does not open a shell or the Docker socket. Data-tier databases stay a manual provision.
10. Deploy one sample app on a hostname under an allowed domain. Check: the exposed hostname returns the app, and an unexposed service does not. Optional `EDGE_ACME_EMAIL`, `EDGE_HTTP_PORT`, and `EDGE_HTTPS_PORT` control the Traefik container created by prepare.

## Do

- Keep 80 and 443 forwarded only to Traefik once that container exists.
- Keep tenant containers on a private app network, with only the exposed service on the edge network.
- Create every user explicitly.
- Store secret values only through the ContainerControl secret form. PostgreSQL keeps the name, path, and injection mode.
- Keep production databases on the data-tier VM.

## Do not

- Do not install Docker Desktop on the server or publish the Docker socket.
- Do not give developers a VM login, a Docker socket, or the Infisical machine credential.
- Do not put secret values in git, compose files, or PostgreSQL.
- Do not turn on self-registration or run the Development user seed in production.
- Do not add Portainer, Keycloak, or a commercial control-panel product.
