# Compose subset

ContainerControl accepts a small compose file and turns it into Engine API calls. It does not leave the file for Docker Compose to run.

Allowed:

- `image`
- `command`
- `environment` for non-secret configuration. A `${SECRET}` or `$SECRET` placeholder is replaced at deploy with the secret of that name for the application's environment. The secret value is not written into the stored compose file.
- `depends_on`
- named volumes
- `x-containercontrol.exposed` and `x-containercontrol.port` to mark a service for Traefik

A `healthcheck` key is not rejected. Deploy does not read it and does not wait for the container to become healthy.

Rejected before any container is created:

- `privileged`
- `network_mode: host`, `pid: host`, `ipc: host`
- `cap_add`, `devices`, `build`
- bind mounts and the Docker socket
- database images (`postgres`, `mysql`, `mariadb`, `mongo`, `mongodb`, `mssql`, `sqlserver`, and the other data-tier names in `ComposePolicy`)

Redis, Valkey, and Memcached are caches, not database images. A single-container app can omit the compose file and set an image instead. Exposed services also join the `edge` network. Every service joins that app's private network with an alias equal to the compose service name.
