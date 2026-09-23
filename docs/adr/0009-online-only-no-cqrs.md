# ADR 0009: Online-only Angular and no CQRS

## Status
Accepted

## Context
Deploys need the management network. Offline use, a PWA, and Ionic are out of scope. Reads and writes do not diverge enough to justify a second model.

## Decision
The Angular app is online-only. There is no service worker, no PWA, no Ionic, and no Nx. The API uses application services inside each module. There is no CQRS and no MediatR.

## Consequences
The browser cannot deploy or read status while it is offline. Adding a mediator later would violate this decision.
