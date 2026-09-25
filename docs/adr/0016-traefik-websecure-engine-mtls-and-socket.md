# ADR 0016: Traefik websecure, Engine mTLS, and the host socket

## Status
Accepted

## Context
Three platform findings share one edge and one Engine trust boundary.

ADR 0004 runs Traefik with the Docker provider and the host local socket, and puts exposed services on the HTTP `web` entrypoint. When `EDGE_ACME_EMAIL` is set, prepare turns on the Let's Encrypt HTTP challenge. Public Host routers are still not marked TLS, and HTTP is not redirected. A local demo has no ACME email and must keep working on HTTP.

ADR 0012 allow-lists `unix`, `npipe`, and `tcp`. It does not require certificates for `tcp://`. ADR 0002 expects the Engine API over mutual TLS and forbids publishing the raw socket. Cleartext `tcp://` outside Development is the gap. `unix` and `npipe` sit on the machine that already trusts the control plane.

Tenant compose already rejects a Docker socket mount. See [compose policy](../compose-policy.md). A dedicated socket proxy in front of Traefik was raised beside that rule. v1 does not need a new compose policy or a proxy.

The product still does not write DNS records and does not buy or install a commercial certificate. That operator work stays in [production setup](../production-setup.md).

## Decision

### PLAT-01 Traefik websecure
When `EDGE_ACME_EMAIL` is set, or an explicit HTTPS edge configuration is present, prepare enables the Traefik `websecure` entrypoint and stamps TLS on the public Host routers for exposed services. In that mode, HTTP on the `web` entrypoint may redirect to HTTPS.

A host with no ACME email and no explicit HTTPS edge configuration keeps the HTTP `web` entrypoint. Local and development demos stay on HTTP. TLS is not forced on those hosts.

The product still does not write DNS records and does not obtain a commercial certificate. Let's Encrypt through `EDGE_ACME_EMAIL` remains the only certificate path the product starts. Public hostnames only, as in ADR 0004.

### PLAT-02 Engine mTLS for tcp://
This extends ADR 0012. The allowed schemes stay `unix`, `npipe`, and `tcp`.

`unix` and `npipe` are unchanged. They stay on the local trust boundary and do not gain a certificate check.

For `tcp://` outside the Development environment, registration and connect require client certificate material from Infisical. The host keeps a certificate reference (path and environment), the same pattern as other Infisical references. PostgreSQL stores that reference. It does not store the certificate material. Cleartext `tcp://` is rejected on registration and on connect with one fixed message. The message does not include the supplied address. A rejected address is not stored.

The Development environment may keep cleartext `tcp://` so a nested or demo Engine still registers and connects.

### PLAT-04 Traefik docker.sock
ADR 0004 stands for v1. Traefik uses the Docker provider and the host local socket. Tenant compose continues to reject Docker socket mounts. This record adds no compose rule.

A dedicated Docker socket proxy for Traefik is deferred. It is a follow-up, not a v1 control, and not a Critical for this wave.

## Consequences
Operators who set `EDGE_ACME_EMAIL`, or who set an explicit HTTPS edge configuration, get `websecure` and TLS on exposed public Host routers. HTTP may redirect to HTTPS in that mode. Operators who leave HTTPS unset keep the HTTP `web` entrypoint, including localhost demos. DNS writes and commercial certificates stay outside the product.

A `tcp://` host outside Development is stored and connected only when the Infisical certificate reference resolves to client material. Cleartext `tcp://` fails with a fixed message that omits the address. `unix:///var/run/docker.sock` and `npipe://./pipe/docker_engine` are unchanged. Development may still use cleartext `tcp://` for a nested or demo Engine.

Traefik on the Docker host keeps the local socket mount from ADR 0004. Tenant services cannot mount that socket. Replacing Traefik's mount with a socket proxy is later work.

PLAT-01 and PLAT-02 are the implementation stories that follow this record. PLAT-04 is documentation-complete for v1: the socket rule already rejects tenant mounts, and the proxy stays out of scope.

## Non-goals
- DNS writes, and buying or installing commercial certificates.
- A Docker socket proxy in front of Traefik.
- Certificate checks on `unix` and `npipe`.
- TLS on an HTTP-only localhost demo.
