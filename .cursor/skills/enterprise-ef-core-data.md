---
name: Enterprise EF Core data
description: Use when designing or implementing EF Core persistence, migrations, transactions, concurrency, and module-owned data for a .NET Modular Monolith — including version columns used by PWA sync.
---
# Enterprise EF Core data

## Goal
Persist module data with EF Core in a way that is migration-safe, concurrency-aware, and readable — one database until a split is justified.

## When to use
Scaffolding or changing DbContexts, mappings, migrations, queries, seed data, or sync/version columns. Auth still owns Identity tables. Architecture owns SQL vs document store per module.

## Ownership

- **.NET API** implements this skill.
- **Architecture** chooses SQL Server vs PostgreSQL (and document stores only per module need).
- **Testing** uses Testcontainers / LocalDB for integration; not EF InMemory for persistence behavior.
- **PWA skill** consumes `rowversion` / ETag / change cursors defined here.

## Defaults

- EF Core on .NET 8+ / current LTS; nullable reference types
- **DbContext per module** (or clearly partitioned sets) in that module’s Infrastructure; the host registers all contexts. No cross-module `DbSet` writes.
- One physical database, module-owned schemas or table prefixes, until an ADR says otherwise
- Migrations per module context; expand/contract for breaking column changes
- No lazy loading proxies by default; explicit `Include` / projection
- No generic repository over `DbSet` unless it adds a real boundary; prefer DbContext + query objects in Application
- Transactions: `IDbContextTransaction` or `ExecutionStrategy` for retries on SQL transient errors
- Concurrency: `rowversion` / `xmin` / `xmin`-equivalent token on entities that sync or take concurrent edits
- Soft delete only with a global query filter **and** an explicit ignore path for admin; do not surprise reporters
- Seed: Development/test only (`IHostEnvironment`). `HasData` for static catalog rows (permissions); runtime seeder for sample users/docs
- Open-source / built-in only (`Microsoft.EntityFrameworkCore.*` providers). No commercial ODMs.

## CLI

- `dotnet ef migrations add <Name> --project <Infrastructure> --startup-project <Host>`
- `dotnet ef database update` locally
- Never hand-edit a snapshot to “fix” a model; add a new migration

## Steps

1. Confirm the module’s aggregates and which tables they own. Reject hidden joins that write another module’s tables.
2. Configure mappings (`IEntityTypeConfiguration`), indexes for actual query filters, and value conversions only when needed.
3. Add concurrency tokens on shared/sync entities. Expose ETag or `rowversion` on GET/PUT if the Angular/PWA client will retry.
4. For PWA delta APIs: a monotonically increasing `xmin`/`rowversion` or `UpdatedAtUtc` **plus** stable id; document cursor semantics. Prefer rowversion over wall clock.
5. Wrap multi-table use cases in a transaction; keep transactions short (no HTTP calls inside).
6. Pagination at the query: `Skip/Take` with a stable order; cap page size. No unbounded `ToListAsync` on user-facing lists.
7. Integration test with WebApplicationFactory + Testcontainers (or LocalDB). Assert migrations apply on a clean database.
8. Document connection string **names**, migration command, and that production seeders are disabled.

## Quality bar

- No connection strings in source
- No string-concat SQL; if SQL is required, parameterized `FromSql`
- N+1 hunted on list endpoints (project to DTOs)
- Migrations are the only schema change path
- Idempotency keys for PWA outbox POSTs live in a module table with a unique index

## Report back

- Contexts, schemas, migration paths
- Concurrency/sync cursor choice
- Seed vs HasData split
- How to migrate locally
- Open questions (provider, split database)
