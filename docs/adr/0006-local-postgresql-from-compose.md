# ADR 0006: Local PostgreSQL comes from Docker Compose

## Status
Accepted

## Context
Identity, schemas, and concurrency have to match production. A second local engine such as SQLite or SQL Server would diverge from PostgreSQL.

## Decision
`deploy/local/compose.yaml` starts PostgreSQL for local development and tests. SQLite is not a second local database. SQL Server is not used.

## Consequences
Docker Engine is a local prerequisite. The setup scripts start only the PostgreSQL service. Integration tests use Testcontainers against the same engine.
