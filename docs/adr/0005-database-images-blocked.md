# ADR 0005: Database images blocked; data tier is separate

## Status
Accepted

## Context
Production databases are admin-provisioned on their own VMs. Letting a compose file start PostgreSQL, MySQL, SQL Server, MongoDB, or a similar database image would put tenant data on the app host.

## Decision
Compose policy rejects database images. Developers receive connection strings as Infisical references. The data tier is not a container on the application network.

## Consequences
The policy check is a later slice. Local control-plane PostgreSQL is the exception: it is the control-plane database, started by Compose, and it is not an application workload.
