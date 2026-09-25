# Observability

Serilog writes structured console logs. OpenTelemetry exports traces and metrics with OTLP. The default collector endpoint is `http://localhost:4317`.

| Name | Purpose |
| --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP endpoint. Default `http://localhost:4317`. |
| `OTEL_SDK_DISABLED` | Set to `true` to turn the SDK off. Tests do this. |

Correlation header: `X-Correlation-ID`. The API generates one when the request does not send it, logs it, and returns it. Unexpected errors include `correlationId` on the problem-details body. Passwords, cookies, and anti-forgery tokens are not written to logs.

Health:

- `GET /health/live` — process only.
- `GET /health/ready` — PostgreSQL. A down database fails readiness and does not fail liveness.

Application log output is not written to the control-plane log. `GET /apps/{id}/logs` returns a one-shot tail from the Docker daemon. Live lines use the SignalR hub `/hubs/logs`: the client calls `Tail` and receives `log` events. That stream stops when the browser disconnects.

Stored lines are a second copy in the `runtime.log_lines` table. A background collector reads timestamped Engine logs and keeps them after the daemon rotates its own files. `GET /apps/{id}/logs/stored` returns those lines for a caller with `runtime.logs.read` and team membership. Retention is `Logs:RetentionDays` (environment `LOGS_RETENTION_DAYS`, default 14). Known secret values are replaced with `[redacted]` before insert. The collector does not write secret values to the control-plane log.

Alerts fire when a deploy fails or a running service healthcheck becomes unhealthy. `PUT /platform/alerts` (`platform.settings.manage`) stores an http(s) webhook URL and comma-separated recipients. The webhook body is `kind`, `applicationId`, `applicationName`, `message`, and `at`. Mail is sent only when `ALERTS_SMTP_HOST` and `ALERTS_SMTP_FROM` are set on the management host. The SMTP password stays in `ALERTS_SMTP_PASSWORD` and is not stored in PostgreSQL or returned by the API. Unconfigured alerts are skipped. A failed notification does not fail the deploy.

The local collector is the Compose profile `observability`. `scripts/dev-setup` does not start it.

```bash
docker compose -f deploy/local/compose.yaml --profile observability up -d otel-collector
```
