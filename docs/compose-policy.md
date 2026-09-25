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

When the application's team has a quota, every service must declare all three limits and the sum must fit. CPU and memory are also set on the container. Storage is counted against the team quota. It is not sent as a Docker storage option. A team with no quota can still omit the limits.

Deploy starts dependency services first. When a service defines a healthcheck, that check is set on the container and the next service waits until the status is healthy. `disable: true` or a `NONE` test skips the wait. A cycle, a missing dependency, or a healthcheck that becomes unhealthy rejects the deploy. The wait is capped at five minutes.

Rejected before any container is created:

- `privileged`
- `network_mode: host`, `pid: host`, `ipc: host`
- `cap_add`, `devices`, `build`
- bind mounts and the Docker socket
- database images, unless that application has `AllowDatabaseImages` set

`AllowDatabaseImages` defaults to false. Existing applications stay false. There is no global switch that allows database images. `ComposePolicy` matches product names as whole `-` / `_` tokens in the image name and repository path (the registry host is ignored), so official tags and common vendor tags are rejected together (`postgresql`, `postgis`, `pgvector`, `timescaledb`, `mysql-server`, `mariadb-galera`, `mcr.microsoft.com/mssql/server`, `azure-sql-edge`, Oracle Database editions, and the other data-tier names in `ComposePolicy`). Oracle Linux and client images such as Instant Client are not database images.

A caller with `apps.write` can create and update an application while the flag stays false, and can turn the flag off. Turning it on for that application also requires `platform.settings.manage`. On update, omitting the field leaves the stored value unchanged. Deploy then skips only the database-image check. Privileged mode, host networking, bind mounts, and the other rejections still apply. Changing the flag writes `apps.database-images.allowed` or `apps.database-images.blocked`.

Redis, Valkey, and Memcached are caches, not database images, whether or not the application allows database images. A single-container app can omit the compose file and set an image instead. Exposed services also join the `edge` network. Every service joins that app's private network with an alias equal to the compose service name.
