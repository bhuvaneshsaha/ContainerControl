# ADR 0015: Compose project name from the application

## Status
Accepted

## Context
Docker Desktop groups containers by the Compose project label `com.docker.compose.project`. Deploy creates containers through the Engine API and does not run `docker compose`, so that label was unset and Desktop did not group the stack. Container names stay `cc{appId12}-{service}` so logs, networks, and existing lookups keep working. The application name is the name operators type. A compose file may already set a top-level `name:`.

## Decision
On deploy, read a top-level compose `name:` when the file has one. Otherwise use `Application.Name`. Sanitize either value: lowercase, keep `[a-z0-9_-]`, collapse every other run of characters to one hyphen, and drop separators at the ends. Cap the result at 63 characters. If nothing valid remains, use `cc-app-` plus the first eight characters of the application id.

Stamp these labels on each container ContainerControl creates:

- `com.docker.compose.project`
- `com.docker.compose.service` (the compose service name)
- `com.docker.compose.container-number` = `1`
- `com.docker.compose.oneoff` = `False`

A compose `name:` wins over the application name, including when that compose name sanitizes to the fallback. The container name formula does not change.

Team and environment are not part of this label. They also stay immutable on `PUT /apps/{id}`: the environment selects the deploy lease and the approval rule, and the team is the membership boundary. Name and Docker host are editable. A host change applies on the next deploy; this change does not move containers that are already running.

## Consequences
Desktop shows the sanitized application name, or the sanitized compose `name:`, as the stack group. Two applications can share a project label; container names and the `cc.application` label still separate them. Removing an application (`DELETE /apps/{id}`, `apps.write`) deletes containers with `cc.application={id}`, then the private network `cc-app-{id}` when Docker accepts that delete. A missing network or a container that is already gone is success. A network that still has endpoints is left in place. A held deploy lease refuses the delete with 409.
