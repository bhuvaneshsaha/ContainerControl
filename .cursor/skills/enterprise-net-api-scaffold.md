---
name: Enterprise .NET API scaffold
description: Use when scaffolding or extending an ASP.NET Core enterprise API (simple DDD, Modular Monolith, OpenAPI, health checks). Persistence details are in the EF Core skill. Auth is owned by the Auth specialist.
---
# Enterprise .NET API scaffold

## Goal
Produce a production-ready ASP.NET Core API slice or Modular Monolith skeleton — not a demo stub.

## Defaults (override only with reason)
- .NET 8+ (or current LTS), nullable enabled, implicit usings
- **Modular Monolith** when the app is enterprise or expected to grow: one host, modules = bounded contexts (Domain / Application / Infrastructure per module, or vertical slices inside the module)
- Simple DDD: entities, value objects, aggregates only when invariants need them; application services/handlers as use cases. Avoid extra DDD layers
- Controllers or Minimal APIs consistently within a module
- EF Core + migrations — follow `skills/enterprise-ef-core-data.md` (do not invent a second data style)
- Observability: Serilog (OSS) or built-in structured logging, correlation id, `/health/live` and `/health/ready` — `skills/enterprise-observability.md`
- HTTP clients: `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` (built-in) for outbound calls; no paid resilience products
- API versioning only when there are external/breaking consumers (`Asp.Versioning.Http`, OSS); skip for a first private Modular Monolith + Angular client
- PWA sync endpoints only if Architecture chose offline mutations — `skills/enterprise-pwa-offline.md`
- FluentValidation (OSS) or DataAnnotations + problem details for input
- AuthN: do not implement here — wait for `agents/08-auth.md` after the user chooses Cookie, JWT, or OIDC
- AuthZ: permission checks at endpoints (`skills/enterprise-permission-based-access.md`); never `[Authorize(Roles = ...)]`
- CQRS: only if justified; follow `skills/enterprise-cqrs.md` — **no MediatR or similar libraries**
- OpenAPI/Swagger in non-prod
- Global exception handler → RFC 7807 problem details
- CancellationToken on all I/O paths; async all the way
- Open-source or built-in only — no commercial libraries
- Seed dummy/sample data in Development (and test) only — never production

## CLI first (do not hand-write a new project)
Official CLIs produce correct SDK, `csproj`, and SDK defaults. Hand-scaffolding from scratch is slower and often wrong.

1. **Check:** run `dotnet --version` (need .NET 8+ / current LTS SDK).
2. **If present:** create with CLI, then customize:
   - `dotnet new sln`
   - `dotnet new webapi` (or `webapiaot` only if justified) for the host
   - `dotnet new classlib` per module layer (Domain / Application / Infrastructure)
   - `dotnet new xunit` (or `nunit`) for test projects
   - `dotnet sln add`, `dotnet add reference`, `dotnet add package` (OSS only)
   - `dotnet ef migrations add` when EF is in play
3. After CLI output exists, add DDD/module folders, endpoints, seeders — **do not recreate** `.csproj`, `Program.cs` boilerplate, or solution files by hand.
4. **If `dotnet` is missing:** stop. Tell the user to install the .NET SDK (official Microsoft installer / `winget install Microsoft.DotNet.SDK.8` or current LTS). Ask whether to wait. Hand-write project files **only if the user explicitly agrees to a no-CLI fallback**; say so in the report.

Extending an existing solution: still use CLI (`dotnet new`, `dotnet add`, `dotnet ef`) — never invent a second host/`csproj` by hand.

## Steps
1. Confirm bounded contexts/modules, entities, and external integrations.
2. Check `dotnet` CLI; create or extend the host and module **projects** with the commands above (modules depend inward; host composes modules).
3. Add domain model + application commands/queries (in-process dispatcher if CQRS; otherwise application services).
4. Wire Infrastructure per `skills/enterprise-ef-core-data.md`: DbContext, migrations, options pattern for config.
5. Expose API endpoints with validation and OpenAPI metadata; leave **permission** attributes as placeholders until Auth has an answer (permission codes, not role names).
6. Add a Development-only seeder or `HasData` for sample entities (and later dummy users, permission catalog, and sample roles once Auth exists).
7. Add unit tests for domain/application; at least one integration test for the happy path when feasible (Testcontainers/LocalDB, not EF InMemory for persistence).
8. Document run steps (migrate, seed, run, sample env vars) in a short README section — no secrets.
9. Emit the collaboration **Handoff** to Angular (contracts), Auth (placeholders), Testing, and DevOps as needed.

## Quality bar
- No business logic in controllers
- No connection strings or keys in source
- Tests compile and are runnable in CI
- Follow existing solution conventions when extending a repo
- Module public API is explicit; no hidden cross-module table writes
- Readable for developers new to DDD
- Endpoints authorize by permission code, not role name
- New solutions/projects come from `dotnet new`, not hand-written `.csproj`

## Report back
- Paths created/changed
- CLI used (`dotnet --version` + commands) or fallback reason
- How to run and seed locally
- Open decisions / assumptions (including auth TBD if unchosen)
