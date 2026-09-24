# Compose subset

ContainerControl accepts a small compose file and turns it into Engine API calls. It does not leave the file for Docker Compose to run.

Allowed:

- `image`
- `command`
- `environment` for non-secret configuration. A `${SECRET}` or `$SECRET` placeholder is replaced at deploy with the secret of that name for the application's environment. The secret value is not written into the stored compose file.
- `depends_on`, as a list or a map of service names
- named volumes
- `healthcheck` with `test`, `interval`, `timeout`, `retries`, and `start_period`
- `x-containercontrol.exposed` and `x-containercontrol.port` to mark a service for Traefik

Deploy starts dependency services first. When a service defines a healthcheck, that check is set on the container and the next service waits until the status is healthy. `disable: true` or a `NONE` test skips the wait. A cycle, a missing dependency, or a healthcheck that becomes unhealthy rejects the deploy. The wait is capped at five minutes.

Rejected before any container is created:

- `privileged`
- `network_mode: host`, `pid: host`, `ipc: host`
- `cap_add`, `devices`, `build`
- bind mounts and the Docker socket
- database images (`postgres`, `mysql`, `mariadb`, `mongo`, `mongodb`, `mssql`, `sqlserver`, and the other data-tier names in `ComposePolicy`)

Redis, Valkey, and Memcached are caches, not database images. A single-container app can omit the compose file and set an image instead. Exposed services also join the `edge` network. Every service joins that app's private network with an alias equal to the compose service name.
