# ADR 0002: Custom Engine API client instead of Portainer

## Status
Accepted

## Context
Developers must not receive VM or Docker socket access. The control plane has to schedule containers itself. Portainer would add another product and a commercial option the stack does not allow.

## Decision
Talk to Docker Engine with `Docker.DotNet` (MIT) over mutual TLS. Do not shell out to the Docker CLI, do not publish the raw socket, and do not deploy Portainer.

## Consequences
Compose support is a documented subset implemented by this codebase. Engine ping, deploys, and Traefik labels are later slices. This slice does not call the Engine API.
