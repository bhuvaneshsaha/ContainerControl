# ADR 0001: On-premises modular monolith and PostgreSQL

## Status
Accepted

## Context
ContainerControl is an internal control plane. ContainerControl, PostgreSQL, and Infisical run on a dedicated Linux management VM. Application workloads run on a separate Linux Docker host. Public cloud services are out of scope.

## Decision
Ship one ASP.NET Core host and one PostgreSQL database. Each module owns a schema. Modules call each other through in-process interfaces. Azure and AWS services are not used.

## Consequences
The control plane is a single management VM and is not highly available. A later split can move a module out because the schema and public contract already belong to that module.
