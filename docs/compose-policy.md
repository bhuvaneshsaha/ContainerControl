# Compose subset

ContainerControl accepts a small compose file and turns it into Engine API calls. It does not leave the file for Docker Compose to run.

Allowed:

- `image`
- `command`
- `environment` for non-secret configuration
- `depends_on`
- named volumes
- `healthcheck` is parsed and not yet waited on
- `x-containercontrol.exposed` and `x-containercontrol.port` to mark a service for Traefik

Rejected before any container is created:

- `privileged`
- `network_mode: host`, `pid: host`, `ipc: host`
- `cap_add`, `devices`, `build`
- bind mounts and the Docker socket
- database images (`postgres`, `mysql`, `mariadb`, `mongo`, `mongodb`, `mssql`, `sqlserver`, and the other data-tier names in `ComposePolicy`)

Redis, Valkey, and Memcached are caches, not database images. A single-container app can omit the compose file and set an image instead. Exposed services also join the `edge` network. Every service joins that app's private network with an alias equal to the compose service name.
