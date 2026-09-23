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
7. Create the Infisical machine identity after Infisical is running, and put the token in the host environment. Check: the token is present on the host and absent from git. This slice does not call Infisical.
8. Sign in and create real users. Assign roles built from the permission catalog. Check: a developer cannot call `GET /platform/hosts`.
9. Register the Docker host, allowed domains, registries, and data-tier databases. These screens are later slices. Check: the production guide section for that step exists before you rely on it.
10. Deploy one sample app on a hostname under the wildcard. Check: that verification step in the platform plan passes. It is not part of this slice.

## Do

- Keep 80 and 443 forwarded only to Traefik once that container exists.
- Keep tenant containers on a private app network, with only the exposed service on the edge network.
- Create every user explicitly.
- Store secret values only through ContainerControl once that form exists.
- Keep production databases on the data-tier VM.

## Do not

- Do not install Docker Desktop on the server or publish the Docker socket.
- Do not give developers a VM login, a Docker socket, or the Infisical machine credential.
- Do not put secret values in git, compose files, or PostgreSQL.
- Do not turn on self-registration or run the Development user seed in production.
- Do not add Portainer, Keycloak, or a commercial control-panel product.
