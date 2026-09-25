# Compose subset

ContainerControl accepts a small compose file and turns it into Engine API calls. It does not leave the file for Docker Compose to run.

Allowed:

- `image`
- `command`
- `environment` for non-secret configuration. A `${SECRET}` or `$SECRET` placeholder is replaced at deploy only when that secret is assigned to this service. A reference to a catalog secret that is not assigned to this service rejects the deploy. The message names the service and the secret name, not the value. The secret value is not written into the stored compose file. An assignment with no services injects that secret nowhere.
- `depends_on`, as a list or a map of service names
- named volumes
- `healthcheck` with `test`, `interval`, `timeout`, `retries`, and `start_period`
- `deploy.resources.limits` with `cpus`, `memory`, and `storage` (for example `cpus: "0.5"`, `memory: 256M`, `storage: 1G`)
- `x-containercontrol.exposed` and `x-containercontrol.port` to mark a service for Traefik
- `x-containercontrol.hostname` as an optional public DNS name for that service. An exposed service that omits it uses the application hostname. The value is checked with the same rules as the application hostname: one DNS name, no wildcards, and no routing characters. A pasted URL is reduced to its host. Two services in one file cannot use the same public hostname. That includes the same value on two services, and a service hostname that equals the application hostname another exposed service still uses. That deploy is rejected before any container is created. Several exposed services that all omit the field still share the application hostname. A service that is not exposed is not published, even when it sets a hostname or the application has one.

```yaml
services:
  api-a:
    image: nginx:stable
    x-containercontrol:
      exposed: true
      port: 80
      hostname: api-a.apps.localhost
  api-b:
    image: nginx:stable
    x-containercontrol:
      exposed: true
      port: 80
      hostname: api-b.apps.localhost
  worker:
    image: busybox:1.36.1
```

`api-a` and `api-b` each receive a Traefik `Host()` rule. `worker` is not exposed, so it stays on the private network. If `api-a` omitted `hostname`, that service would use the application hostname.

When the application's team has a quota, every service must declare all three limits and the sum must fit. CPU and memory are also set on the container. Storage is counted against the team quota. It is not sent as a Docker storage option. A team with no quota can still omit the limits.

Deploy starts dependency services first. When a service defines a healthcheck, that check is set on the container and the next service waits until the status is healthy. `disable: true` or a `NONE` test skips the wait. A cycle, a missing dependency, or a healthcheck that becomes unhealthy rejects the deploy. The wait is capped at five minutes.

Rejected before any container is created:

- `privileged`
- `network_mode: host`, `pid: host`, `ipc: host`
- `cap_add`, `devices`, `build`
- bind mounts and the Docker socket. The tenant denylist stays enforced. Traefik's read-only host socket, and the accepted residual of Traefik holding it, are [ADR 0016](adr/0016-traefik-websecure-engine-mtls-and-socket.md)
- database images, unless that application has `AllowDatabaseImages` set
- a service hostname that is not a DNS name, or two services that claim the same public hostname

A template stores a compose document only when this policy accepts it and the file contains no secret value. A `${SECRET}` or `$SECRET` placeholder is allowed. A private key, an `env_file`, a compose `secrets` entry, a URL password, or a literal value on a secret-like name is rejected. The rejection does not repeat the value, and the value is not written to PostgreSQL or the log. See [ADR 0017](adr/0017-application-templates.md).

`AllowDatabaseImages` defaults to false. Existing applications stay false. There is no global switch that allows database images. `ComposePolicy` matches product names as whole `-` / `_` tokens in the image name and repository path (the registry host is ignored), so official tags and common vendor tags are rejected together (`postgresql`, `postgis`, `pgvector`, `timescaledb`, `mysql-server`, `mariadb-galera`, `mcr.microsoft.com/mssql/server`, `azure-sql-edge`, Oracle Database editions, and the other data-tier names in `ComposePolicy`). Oracle Linux and client images such as Instant Client are not database images.

A caller with `apps.write` can create and update an application while the flag stays false, and can turn the flag off. Turning it on for that application also requires `platform.settings.manage`. On update, omitting the field leaves the stored value unchanged. Deploy then skips only the database-image check. Privileged mode, host networking, bind mounts, and the other rejections still apply. Changing the flag writes `apps.database-images.allowed` or `apps.database-images.blocked`.

Redis, Valkey, and Memcached are caches, not database images, whether or not the application allows database images. A single-container app can omit the compose file and set an image instead. Exposed services also join the `edge` network. Every service joins that app's private network with an alias equal to the compose service name.

Docker Desktop groups a stack by the Compose project label. On deploy, a top-level compose `name:` is sanitized and used as that project. When the file has no `name:`, the application name is sanitized instead. Sanitizing lowercases the value, keeps `[a-z0-9_-]`, collapses other characters to a single hyphen, and trims separators from the ends. An empty result becomes `cc-app-` plus the first eight characters of the application id. Each container is labeled `com.docker.compose.project`, `com.docker.compose.service`, `com.docker.compose.container-number`, and `com.docker.compose.oneoff`. The container name stays `cc{appId12}-{service}`. See [ADR 0015](adr/0015-compose-project-name.md).
