# ADR 0007: Private app network and a shared edge network

## Status
Accepted

## Context
Containers in one application need to reach each other by compose service name. They must not reach another application's containers. Only explicitly exposed services should be reachable from Traefik.

## Decision
Each application gets a private Docker network. ContainerControl sets the network alias to the compose service name. Traefik and exposed services join one shared `edge` network. Internal services have no host port and no edge attachment. The control plane is not attached to tenant networks.

## Consequences
Service-name DNS works only when the alias is set. Network creation is a later slice. This slice does not create Docker networks.
