# ADR 0013: Deploy leases are per application and environment

## Status
Accepted

## Context
Delivery kept a single `delivery.worker_lease` row for the whole control plane. Any deploy held that row, so a second deploy returned 409 Conflict until the first finished or the five-minute expiry passed. Two applications, or two environments, could not deploy at the same time.

## Decision
A deploy lease is keyed by application id and environment. Deploys run together when the application id or the environment differs. The same application id and the same environment stay single-active.

Acquisition does not wait in the request. If that lease is held and its expiry is still in the future, deploy, approve, rollback, and the CI webhook return 409 Conflict. The problem title is "A deployment is already running for this application and environment." The holder keeps the lease for five minutes (`DeployLease.HoldFor`). A process that stops without releasing the row does not block that pair longer than that. The caller retries; the API does not queue the second deploy.

The lease is released when the deploy returns, including success, rejection, an Engine failure, and cancellation. Release clears the row only when the owner id still matches, so a deploy that outlives the five minutes does not clear a newer holder. If release itself fails, the expiry is the backstop.

Start, stop, and restart do not take this lease.

The migration replaces `delivery.worker_lease`. The previous row had no application, so it is not copied. An in-flight deploy at migration time can overlap once; the next deploy takes the new key.

## Consequences
Operators can deploy different applications, or the same application id in different environments, at the same time. A second deploy of one application and environment should retry after the in-flight deploy finishes, and does not need to wait longer than five minutes when the holder has already stopped.
