# ADR 0008: .NET 10 and Angular 21 with a plain list-and-form UI

## Status
Accepted

## Context
The API targets current .NET and the client is an Angular SPA. The UI should stay easy to scan without a custom visual system or a paid component library.

## Decision
The host targets `net10.0`. The client is an Angular 21 application created with the Angular CLI, on Node.js 22 LTS. Pages are lists and forms: a title, a short nav, and one content region. Actions are text buttons. Empty and error states are one sentence each.

## Consequences
There is no dashboard graphic and no extra component library. Shared controls are documented in `docs/components` when they are reused.
