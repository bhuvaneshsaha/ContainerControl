# ADR 0011: Secrets are injected only into named services

## Status
Accepted

## Context
Deploy listed every secret for a team and environment and passed that full set into every compose service. A multi-container application received secrets in containers that do not use them.

## Decision
Each secret reference stores the compose service names it is injected into, in `applications.secret_service_targets`. Deploy reads a secret value only when at least one service in the current plan is named, and injects it only into those services. An empty target list injects the secret into no service. It does not mean every service.

Saving a secret requires at least one service name. The Applications secrets panel offers those names from the open application's compose file. A single-container application (an image with no compose file, or a compose file with one service) has that service selected. The list shows the assignment, for example `DB_PASSWORD (env) → api, worker`.

Targets are service names for the team and environment. Two applications that both have a service named `api` both receive a secret assigned to `api`. Saving replaces the stored list. The panel keeps target names that are not services of the application currently open, so editing one application does not drop another application's distinct service names.

The migration adds the target table and does not copy existing secrets onto every service. Secrets saved before this change have no targets until an operator saves them again and chooses services. Deploy fails when a service's environment or command references a catalog secret that is not assigned to that service. The error names the service and the secret name. It does not include the value.

## Consequences
Operators assign services before a secret is injected. A deployment that still references an unassigned secret fails until that secret is assigned. That is intentional: the previous behavior put every team environment secret into every container. PostgreSQL still does not store secret values. See ADR 0003.
