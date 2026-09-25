# ADR 0017: Traefik slot swap and canary

## Status
Accepted

## Context
A normal deploy replaces the one running copy. Operators asked for an Azure deployment-slot style release: start the new version beside the live one, then move traffic, and keep the previous version for rollback. A canary should send a percentage of public traffic to the new version. The edge is already Traefik on the Docker host. There is no Azure account and no commercial traffic manager.

## Decision
One replica stays the default. `POST /apps/{id}/deploy` still removes the previous containers and starts one copy.

`POST /apps/{id}/slots/deploy` (`deploy.execute`) is the exception. It starts the new release on the color that is not live (`blue` or `green`) and leaves the live containers running. Each color has its own private network, so service-name DNS stays inside that copy. Exposed services join `edge`. A small `nginx:1.28-alpine` container is the only public route for the hostname. Traefik's Docker provider does not accept a weighted service, so nginx applies the percent between the live copy and the candidate. A weight of zero is left out of that upstream. The Traefik router on the nginx container uses a higher priority than a router left on a classic one-replica container, so public traffic reaches nginx. The first slot deploy, when nothing is live, receives 100 percent because there is no previous release to keep.

`POST /apps/{id}/slots/swap` moves 100 percent to the candidate and records the previous slot. The previous containers stay up. `POST /apps/{id}/slots/canary` sets a percent from 1 to 99 on the candidate and leaves the rest on the live slot. `POST /apps/{id}/slots/revert` (`deploy.rollback`) sends 100 percent back to the previous slot. A normal deploy clears the slot row and both slot networks.

The slot router uses the same hostname check and, when `EDGE_ACME_EMAIL` is set, the same `websecure` TLS labels as a normal router. This does not add a second public IP or an Azure dependency. A slot deploy that is refused, or that fails after a live copy is already running, leaves that application status unchanged. The deployment history still records the refusal. Swap, canary, and revert do the same when they are refused.

## Consequences
Two copies run until a normal deploy or a delete. A team quota counts both copies on a slot deploy. Start, stop, and restart still act on every container of the application, including both slots and the route container. The route container is not an application replica. Stored logs skip it. Alerts and the log store are separate from this routing decision.
