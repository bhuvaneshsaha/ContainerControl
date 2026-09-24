# ADR 0003: Secret values go to Infisical and are not stored again

## Status
Accepted

## Context
Application secrets cannot live in git, compose files, or the control-plane database. Infisical (Apache-2.0) is the secrets store.

## Decision
Create and update send the value through ContainerControl to Infisical. PostgreSQL stores the path, environment, and injection mode only. The API and UI never return the current value. Audit records the path and the actor, not the value. Machine credentials that bootstrap Infisical stay in host configuration, split by environment.

## Consequences
This slice stores no secret values and does not call Infisical. The round-trip is a later slice. Operators still create the Infisical machine identity by hand.
