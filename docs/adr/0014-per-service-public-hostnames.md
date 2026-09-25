# ADR 0014: Per-service public hostnames

## Status
Accepted

## Context
An application has one public hostname. Traefik labels for every exposed service use that name. A compose file with more than one public service needs a distinct `Host()` rule per service, for example `api-a.apps.localhost` and `api-b.apps.localhost`. A single exposed service that does not set its own name still has to use the application hostname. See ADR 0004.

## Decision
A service may set `x-containercontrol.hostname`. Deploy reads it with `PublicHostname`: the same canonical DNS form, and the same rejection of wildcards and routing characters. An exposed service with a port that omits the field uses the application hostname. A service that is not exposed is not published. Setting a hostname does not publish it, and an application hostname does not publish it either.

Two services in one compose file cannot use the same public hostname. That is the same `x-containercontrol.hostname` on two services, or a service hostname equal to the application hostname of another exposed service that did not set its own. The deploy returns 400 before any Engine call. Several exposed services that all omit the field still share the application hostname.

Each hostname that is routed must sit under an allowed domain. The check is the one already used for the application hostname.

## Consequences
Traefik receives one `Host()` label per exposed service that has a port. Operators who want two public names put both under a wildcard that already points at the host, then set the compose field. The application hostname remains the fallback. A service with no hostname and no application hostname stays off the edge network.
