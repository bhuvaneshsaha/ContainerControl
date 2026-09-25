# ADR 0016: Traefik websecure, Engine mTLS, and the host socket

## Status
Accepted

## Context
Three platform findings share one edge and one Engine trust boundary. Platform and App Sec locked the acceptance bars in this record.

ADR 0004 runs Traefik with the Docker provider and the host local socket, and puts exposed services on the HTTP `web` entrypoint. When `EDGE_ACME_EMAIL` (`Edge:AcmeEmail`) is set, prepare turns on the Let's Encrypt HTTP challenge. Public Host routers are still not marked TLS, and HTTP is not redirected. A local demo has no ACME email and must keep working on HTTP.

The Host() rule is already `PublicHostname.TraefikHostRule` (APP-01 and APP-05). TLS has to use that same validated rule. It does not get a second hostname path.

ADR 0012 allow-lists `unix`, `npipe`, and `tcp`. It does not require certificates for `tcp://`. ADR 0002 expects the Engine API over mutual TLS and forbids publishing the raw socket. Cleartext `tcp://` outside Development is the gap. `unix` and `npipe` sit on the machine that already trusts the control plane.

Tenant compose already rejects a Docker socket mount. See [compose policy](../compose-policy.md). A socket proxy or a Traefik file provider was raised beside that rule. v1 does not need a new compose policy, a proxy, or a file provider.

The product still does not write DNS records. Commercial certificates stay with the operator. [Production setup](../production-setup.md) is that checklist. The catalog code `edge.certs.manage` is not an upload path in this wave.

## Decision

### PLAT-01 Traefik websecure
HTTPS is gated on `Edge:AcmeEmail` / `EDGE_ACME_EMAIL`.

When that value is set, the router that already uses `PublicHostname.TraefikHostRule` emits all of the following on that same router:

- entrypoint `websecure`
- `tls=true`
- `tls.certresolver=le`

The rule string stays the single Host matcher returned by `PublicHostname.TraefikHostRule`. APP-01 and APP-05 still own that path. TLS labels do not build a second matcher.

When ACME is on, HTTP on the `web` entrypoint redirects to HTTPS.

When ACME is unset, routers stay on `web` only. They do not get `websecure`, `tls=true`, or `tls.certresolver`. HTTP is not redirected. Local and development demos stay on that HTTP entrypoint.

Let's Encrypt through `EDGE_ACME_EMAIL` is the only certificate path the product starts. Public hostnames only, as in ADR 0004. The product does not write DNS records.

### PLAT-02 Engine mTLS for tcp://
This extends ADR 0012. The allowed schemes stay `unix`, `npipe`, and `tcp`.

`unix` and `npipe` are unchanged. They stay on the local trust boundary and do not gain a certificate check.

For `tcp://` outside the Development environment, registration and connect require the client certificate, the client key, and the CA. That material comes from Infisical or from path references stored on the host row. PostgreSQL stores those references. It does not store PEM. Cleartext `tcp://` is rejected on registration and on connect with one fixed message. The message does not include the supplied address. A rejected address is not stored.

The Development environment may keep cleartext `tcp://` so a nested or demo Engine still registers and connects. v1 has no separate lab insecure flag. Development is the only cleartext exception.

### PLAT-04 Traefik docker.sock
ADR 0004 stands for v1. Traefik keeps the Docker provider and the host socket mounted read-only (`:ro`).

The tenant compose Docker socket denylist stays enforced. This record adds no compose rule.

A Docker socket proxy and a Traefik file provider are deferred. They are follow-ups, not v1 controls, and not a Critical for this wave. Recording the residual below does not reopen PLAT-04. PLAT-04 is documentation-complete for v1.

### Commercial certificates
Commercial certificates stay with the operator. This wave does not implement `edge.certs.manage`. The product does not upload or replace a commercial certificate. The operator obtains one when Let's Encrypt cannot issue, as in production setup.

## Accepted residual
Traefik holding the host Docker socket is an accepted high-value v1 risk. The container can see the Engine through that read-only mount until a socket proxy or the file provider lands. That residual is closed by the deferred follow-up. It is not a reason to reopen PLAT-04.

## Consequences
Operators who set `EDGE_ACME_EMAIL` get `websecure`, `tls=true`, and `tls.certresolver=le` on the same validated Host() router, and HTTP redirects to HTTPS. Operators who leave ACME unset keep `web` only, including localhost demos. DNS writes stay outside the product. Commercial certificates stay outside the product, and `edge.certs.manage` is not built in this wave.

A `tcp://` host outside Development is stored and connected only when the client certificate, key, and CA are available from Infisical or from path references on the host row. The database holds references, not PEM. Cleartext `tcp://` fails with a fixed message that omits the address. `unix:///var/run/docker.sock` and `npipe://./pipe/docker_engine` are unchanged. Development may still use cleartext `tcp://`. There is no lab insecure flag to turn that on in any other environment.

Traefik on the Docker host keeps the read-only local socket mount from ADR 0004. Tenant services cannot mount that socket; the compose denylist stays enforced. A socket proxy and the file provider are later work. Until they land, Traefik's hold on the host socket remains the accepted high-value residual above.

PLAT-02 is implemented: registration and connect require a client certificate, key, and CA reference for `tcp://` outside Development. Those references are `file:` paths or `infisical:` paths. The database does not store PEM. PLAT-01 is the websecure label story. PLAT-04 is documentation-complete for v1.

## Non-goals
- DNS writes.
- `edge.certs.manage`, and any product upload of a commercial certificate, in this wave.
- A lab insecure flag for Engine TLS.
- PEM stored in PostgreSQL.
- A Docker socket proxy or a Traefik file provider.
- Certificate checks on `unix` and `npipe`.
- TLS on an HTTP-only edge that has no ACME email.
