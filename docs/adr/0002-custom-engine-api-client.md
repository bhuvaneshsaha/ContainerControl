# ADR 0002: Custom Engine API client instead of Portainer

## Status
Accepted

## Context
Developers must not receive VM or Docker socket access. The control plane has to schedule containers itself. Portainer would add another product and a commercial option the stack does not allow.

## Decision
Talk to Docker Engine with `Docker.DotNet` (MIT) over mutual TLS. Do not shell out to the Docker CLI, do not publish the raw socket, and do not deploy Portainer.

## Consequences
The host calls the Engine API for version ping, image pull, networks, container create, start, stop, and restart, logs, and stats. Deploy turns the documented compose subset into those calls and sets Traefik Host labels on exposed services. Operators install Engine and mutual TLS on the Docker host. The product does not publish the raw socket and does not deploy Portainer.
