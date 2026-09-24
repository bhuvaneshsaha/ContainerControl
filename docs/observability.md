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

Application log output is not written to the control-plane log. `GET /apps/{id}/logs` returns a one-shot tail. Live lines use the SignalR hub `/hubs/logs`: the client calls `Tail` and receives `log` events. That stream stops when the browser disconnects. There is no long-term log store.

The local collector is the Compose profile `observability`. `scripts/dev-setup` does not start it.

```bash
docker compose -f deploy/local/compose.yaml --profile observability up -d otel-collector
```
