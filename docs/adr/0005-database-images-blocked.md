# ADR 0005: Database images blocked by default; data tier is separate

## Status
Accepted

## Context
Production databases are admin-provisioned on their own VMs. Letting a compose file start PostgreSQL, MySQL, SQL Server, MongoDB, or a similar database image would put tenant data on the app host.

## Decision
Database images are blocked by default. An application may set `AllowDatabaseImages` so its own deploy skips that denylist. Turning the flag on requires `platform.settings.manage`. Turning it off requires `apps.write`. There is no global switch. Developers receive connection strings as Infisical references. The data tier remains the default place for tenant data.

## Consequences
Compose policy rejects database images before any container is created, unless that application has `AllowDatabaseImages` set. The flag defaults to false, including for applications that already exist. The check matches product tokens on the repository path, including official images and common vendor tags (Bitnami `postgresql`, PostGIS, pgvector, TimescaleDB, `mssql/server`, and `azure-sql-edge`). When the flag is on, only that check is skipped. Privileged mode, host networking, bind mounts, and the other compose rejections still apply. Redis, Valkey, and Memcached stay allowed either way. Oracle Linux is not a database image. A change to the flag is audited. Developers still receive connection strings as secret references. The data tier stays off the application network unless an administrator allows database images for that one application. Local control-plane PostgreSQL is the exception: Compose starts it for the control plane, and it is not an application workload.
