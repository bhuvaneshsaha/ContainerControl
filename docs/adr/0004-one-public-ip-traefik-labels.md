# ADR 0004: One public IP, wildcard DNS, Traefik labels

## Status
Accepted

## Context
Many application hostnames must share one static public IP. Per-app DNS writes and Docker Swarm are not wanted in v1.

## Decision
The admin points a wildcard, such as `*.apps.example.com`, and any extra apex domain at that IP, and forwards ports 80 and 443 to Traefik. Traefik runs as a container on the Docker host, using the Docker provider and the local socket. ContainerControl attaches labels for exposed services. It does not write DNS records. A second Docker host is not behind this Traefik in v1.

## Consequences
Preparing a host creates the `edge` network and the `cc-traefik` container. Exposed services receive Traefik Host labels on the web entrypoint. A brand-new apex domain still needs one DNS record at the provider and an allowed-domain entry in the product. When `EDGE_ACME_EMAIL` is set, prepare enables Traefik's Let's Encrypt HTTP challenge. That resolver covers public hostnames only. `websecure`, `tls=true`, `tls.certresolver=le`, and the HTTP redirect for that case are [ADR 0016](0016-traefik-websecure-engine-mtls-and-socket.md). The product does not write DNS records and does not install a certificate obtained outside Let's Encrypt. The read-only Traefik socket is an accepted high-value v1 risk in that same record. A second Docker host is not behind this Traefik. Do not start the local Traefik Compose profile and prepare the same host at the same time.
