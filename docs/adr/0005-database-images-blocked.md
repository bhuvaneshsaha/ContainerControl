# ADR 0005: Database images blocked; data tier is separate

## Status
Accepted

## Context
Production databases are admin-provisioned on their own VMs. Letting a compose file start PostgreSQL, MySQL, SQL Server, MongoDB, or a similar database image would put tenant data on the app host.

## Decision
Compose policy rejects database images. Developers receive connection strings as Infisical references. The data tier is not a container on the application network.

## Consequences
Compose policy rejects database images before any container is created. The check matches product tokens on the repository path, including official images and common vendor tags (Bitnami `postgresql`, PostGIS, pgvector, TimescaleDB, `mssql/server`, and `azure-sql-edge`). Redis, Valkey, and Memcached stay allowed. Oracle Linux is not a database image. Developers still receive connection strings as secret references. The data tier stays off the application network. Local control-plane PostgreSQL is the exception: Compose starts it for the control plane, and it is not an application workload.
