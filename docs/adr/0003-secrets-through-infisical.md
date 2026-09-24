# ADR 0003: Secret values go to Infisical and are not stored again

## Status
Accepted

## Context
Application secrets cannot live in git, compose files, or the control-plane database. Infisical (Apache-2.0) is the secrets store.

## Decision
Create and update send the value through ContainerControl to Infisical. PostgreSQL stores the path, environment, and injection mode only. The API and UI never return the current value. Audit records the path and the actor, not the value. Machine credentials that bootstrap Infisical stay in host configuration, split by environment.

## Consequences
When `INFISICAL_SITE_URL` and that environment's client id, client secret, and project id are set, create, update, and deploy read and write the value through the Infisical API. PostgreSQL keeps the path, environment, and injection mode. The API and UI do not return the value. In Development only, if those credentials are absent, the same calls use files under `deploy/local/secret-store/` (gitignored). Any other environment fails the call until the machine identity exists. Operators still create that identity by hand.
