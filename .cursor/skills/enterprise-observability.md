---
name: Enterprise observability
description: Use when wiring logs, traces, metrics, health checks, and correlation across the .NET API, Angular/PWA client, and the chosen host (On-prem / Azure / AWS). Not a substitute for DevOps pipelines.
---
# Enterprise observability

## Goal
Make production behavior diagnosable: structured logs, distributed traces, health, and a few golden metrics — OpenTelemetry and platform built-ins, no paid APM products.

## When to use
New host, new module, PWA sync failures, or DevOps wiring exporters. Architecture names the backend (file collector / Azure Monitor / CloudWatch); this skill is how the app emits.

## Ownership

| Piece | Owner |
|-------|--------|
| `Activity`, meters, Serilog/built-in logging in the API | .NET API |
| Correlation id on HTTP (API + Angular interceptor) | .NET + Angular |
| `/health` liveness vs readiness (DB, downstream) | .NET API |
| Exporters, dashboards, log retention in the platform | DevOps + platform skill |
| Client error sink (no secrets, no PII by default) | Angular; Security reviews |
| What “good” looks like for a slice | Master / slice DoD in collaboration skill |

## Defaults

- **OpenTelemetry** (OSS) for traces and metrics in ASP.NET Core
- Logs: Serilog **or** `Microsoft.Extensions.Logging` with structured templates — never string-interpolate secrets
- Correlation: accept `traceparent` / `X-Correlation-ID`; generate if missing; echo to Angular error logs
- Health: `/health/live` (process) and `/health/ready` (DB + critical deps). Pipelines wait on ready
- Metrics (start small): request duration, error rate, dependency duration, PWA sync conflict count if offline exists
- Angular: intercept HTTP failures; log status + correlation id, not bodies with PII. Optional OTel JS exporter only if Architecture asked — not a premium RUM product
- Azure: OTel → Azure Monitor / App Insights **exporter** is allowed as platform (not a commercial third-party APM)
- AWS: OTel → CloudWatch / X-Ray if already standard
- On-prem: OTel Collector + files or existing OSS stack
- Open-source or platform-built-in only — no New Relic / Datadog / Dynatrace as defaults

## Steps

1. Confirm the platform skill’s observability default.
2. Add correlation middleware and Angular interceptor (same header names).
3. Instrument module boundaries and sync endpoints with spans named after use cases, not generic `Handle`.
4. Split live vs ready health; do not fail liveness because SQL is briefly down (that is readiness).
5. Scrub: tokens, passwords, connection strings, raw auth headers never logged.
6. DevOps: ship exporters and pipeline wait-on-ready. Do not invent dashboard JSON unless the user wants it.
7. Document env var **names** for OTEL endpoints and log levels.

## Quality bar

- A failed request can be found by correlation id in logs
- Health is actually used by CD
- PWA outbox failures are countable
- No PII in client telemetry by default

## Report back

- Header names, health paths, exporters
- What is intentionally not traced
- Residual risk (client telemetry, log volume)
