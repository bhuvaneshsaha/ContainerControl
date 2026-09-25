# ADR 0017: Application templates

## Status
Accepted

## Context
Developers paste a compose document when they register an application. Many of those documents are the same starter. The compose policy already decides which documents can be deployed. Secret values belong in the secret catalog, not in a shared starter.

## Decision
A template is a named starter in the Applications module: a name, a short description, and one compose document. Publishing stores the document only when the existing compose policy accepts it and a scan finds no secret value. The scan rejects private keys, `env_file`, compose `secrets`, URL passwords, and literal values on secret-like names. A `${SECRET}` or `$SECRET` placeholder is kept. Rejection messages do not include the value. The value is not written to PostgreSQL or to logs.

`apps.templates.manage` publishes and removes a template. Listing is allowed for `apps.read`, `apps.write`, or `apps.templates.manage`. `POST /apps/from-template` requires `apps.write` and copies the stored compose onto a new application. A compose document on that request is ignored. Removing a template does not remove applications already created from it.

The catalog is this product's data. There is no external catalog and no payment.

## Consequences
Operators add `apps.templates.manage` to the role that publishes starters. The Development platform administrator role is updated to the full catalog on the next Development seed. The Development developer role is unchanged. Templates cannot turn on database images; that flag stays off on the application created from the template.
