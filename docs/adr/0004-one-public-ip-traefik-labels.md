# ADR 0004: One public IP, wildcard DNS, Traefik labels

## Status
Accepted

## Context
Many application hostnames must share one static public IP. Per-app DNS writes and Docker Swarm are not wanted in v1.

## Decision
The admin points a wildcard, such as `*.apps.example.com`, and any extra apex domain at that IP, and forwards ports 80 and 443 to Traefik. Traefik runs as a container on the Docker host, using the Docker provider and the local socket. ContainerControl attaches labels for exposed services. It does not write DNS records. A second Docker host is not behind this Traefik in v1.

## Consequences
A brand-new apex domain still needs one DNS record and an allowed-domain entry. Names that are not publicly resolvable need an uploaded certificate. Traefik and label routing are later slices. This slice does not start Traefik.
