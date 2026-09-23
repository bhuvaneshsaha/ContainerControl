---
name: Enterprise CQRS
description: Use when implementing CQRS for a .NET Modular Monolith or API without MediatR or similar mediator libraries. Provides a small in-process command/query core applications can extend.
---
# Enterprise CQRS

## Goal
Add a small, readable CQRS core: commands and queries as use cases, handlers in the module, one in-process dispatcher via DI. No MediatR, Wolverine, MassTransit mediator, or other mediator packages.

## When to use
When reads and writes genuinely diverge (different models, permissions, or scaling) or the team wants explicit use-case types. Skip CQRS for tiny CRUD modules — an application service is enough.

## Core abstraction (keep this tiny)
Define in a shared kernel or building-blocks project — interfaces only, no pipeline DSL:

- `ICommand` / `ICommand<TResult>`
- `IQuery<TResult>`
- `ICommandHandler<TCommand>` / `ICommandHandler<TCommand, TResult>`
- `IQueryHandler<TQuery, TResult>`
- `IDispatcher` (or `ICommandDispatcher` + `IQueryDispatcher`): resolve the handler from `IServiceProvider` and invoke it

Applications extend by adding handlers, not by growing the core. Optional behaviors (logging, validation) are explicit decorator classes registered in DI — not a magic pipeline.

## Defaults
- Handlers live in the owning module's Application layer
- Commands change state; queries do not
- No event sourcing, domain-event bus, or server outbox unless the user asks. (Client PWA outbox is `skills/enterprise-pwa-offline.md` — not CQRS.)
- Validation at the handler or via FluentValidation before dispatch
- CancellationToken on every handler
- Open-source / built-in only (`Microsoft.Extensions.DependencyInjection`)

## Steps
1. Confirm CQRS is justified; otherwise use application services.
2. Add the core interfaces + dispatcher (one file set; keep it obvious for DDD newcomers).
3. Register handlers by convention or explicit `AddScoped` — prefer explicit if the team is new.
4. Implement the first command and query in one module as the template.
5. Call the dispatcher from API endpoints; no business logic in controllers.
6. Unit-test handlers; one integration test through the dispatcher.

## Quality bar
- A newcomer can find "this use case" by type name
- No generic `Handle(object)` service locator soup beyond the thin dispatcher
- Modules do not call another module's handlers internally — go through that module's public API
- Zero MediatR (or similar) package references

## Report back
- Core types and registration
- Example command/query paths
- Why CQRS was (or was not) applied
