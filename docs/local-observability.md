# Local Observability Baseline

OBS001 wires OpenTelemetry tracing into all FPS .NET services so that smoke requests produce inspectable traces in a local Jaeger instance.

## Prerequisites

Start the local infrastructure stack (includes Jaeger):

```bash
docker compose -f code/infrastructure/docker-compose.yaml up -d jaeger
```

## Trace UI

Open **http://localhost:16686** in your browser. This is the Jaeger UI.

## Finding a trace for a failed request

1. Make a request through Envoy (e.g. `curl http://localhost:10000/me -H "Authorization: Bearer $TOKEN"`)
2. In Jaeger UI → **Search** → select **Service** (e.g. `fps-identity`) → click **Find Traces**
3. Click a trace to see the span waterfall. Failed requests show as error spans (red).

Each service emits:
- An incoming HTTP span for every request it handles (`fps-<service>` service name)
- An outgoing HTTP span for every backend call it makes

## Log correlation

Each service logs a line per request containing `TraceId` and `SpanId`:

```
info: FPS.Request[0]
      GET /me TraceId=4bf92f3577b34da6a3ce929d0e0e4736 SpanId=00f067aa0ba902b7
```

Use the `TraceId` value to find the corresponding trace in Jaeger UI.

## OTLP endpoint configuration

By default services export to `http://localhost:4318` (Jaeger OTLP HTTP port). Override with:

```bash
# In shell or .env.local
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
```

Or in `appsettings.json` / `appsettings.Development.json`:

```json
{
  "Otlp": {
    "Endpoint": "http://localhost:4318"
  }
}
```

## Services instrumented

| Service | Name in Jaeger |
|---------|---------------|
| Identity | `fps-identity` |
| Booking | `fps-booking` |
| Profile | `fps-profile` |
| Configuration | `fps-configuration` |
| Reporting | `fps-reporting` |
| Audit | `fps-audit` |
| Notification | `fps-notification` |
| Customer | `fps-customer` |

## What is NOT logged

No secrets, bearer tokens, or tenant data are included in trace attributes or log messages. The OTLP exporter sends span metadata only (method, path, status, duration, error flag).
