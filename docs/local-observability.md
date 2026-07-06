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
2. In Jaeger UI → **Search** → select **Service** (e.g. `fairspot-identity`) → click **Find Traces**
3. Click a trace to see the span waterfall. Failed requests show as error spans (red).

Each service emits:
- An incoming HTTP span for every request it handles (`fps-<service>` service name)
- An outgoing HTTP span for every backend call it makes

## Log correlation

Each service logs a line per request containing `TraceId`, `SpanId`, and the `tenant_id` operator dimension:

```
info: FPS.Request[0]
      GET /me TraceId=4bf92f3577b34da6a3ce929d0e0e4736 SpanId=00f067aa0ba902b7 tenant_id=greenlogistics
```

Use the `TraceId` value to find the corresponding trace in Jaeger UI.

## Tenant observability dimension (`tenant_id`)

`tenant_id` is a technical/operator dimension so operators can filter live logs and traces by tenant — it is not a business report and not billing (see the DataHub usage ledger for that).

- **Source of truth (trusted only):** the value comes from the validated JWT tenant claim via `ICurrentUser` for HTTP requests, or a Dapr-delivered event envelope's tenant for internal handlers. It is **never** read from a request body, query string, or header supplied by an external caller, so a forged `tenant_id` header cannot poison telemetry.
- **Where it appears:** the `FPS.Request` log line (above) and, for the same request/event, an OpenTelemetry span attribute `tenant_id`. Find a tenant's traces in Jaeger by adding the tag `tenant_id=<tenant>` to the search.
- **Platform / no-tenant requests** (health checks, `/platform/*` platform-plane endpoints, unauthenticated intake, internal schedulers with no tenant): the log carries the sentinel `tenant_id=__none__` and the span attribute is left unset. `__none__` cannot be a real tenant id (tenant ids are lowercase alphanumeric + hyphens, no underscores), so operators select customer traffic with `tenant_id != "__none__"`.
- **Event-driven spans:** the Audit (`booking-events`, `tenant-reset-events`) and DataHub projection handlers tag their processing span with the trusted envelope tenant.

## Business activity correlation

Technical log correlation is not the same as business audit.

When a request creates a business-relevant action, the application should copy the current OpenTelemetry identifiers into the domain event or audit command:

```csharp
var traceId = Activity.Current?.TraceId.ToString();
var spanId = Activity.Current?.SpanId.ToString();
```

The Audit service can then store those values on the business activity record. This lets an authorized operator move from an audit record to the matching technical trace during support or incident handling.

Rules:

- `traceId` and `spanId` are optional correlation metadata.
- Business audit must still work when no trace is active.
- Audit records still need tenant, actor hash, action, entity, result, reason, timestamp, and source event ID.
- HR/admin/auditor business screens should read Audit records, not raw Grafana/Loki logs.
- Technical logs must not contain raw user IDs, actor IDs, recipient IDs, names, emails, license plates, tokens, or full request payloads.

## OTLP endpoint configuration

By default services export to `http://localhost:4318/v1/traces` (Jaeger OTLP HTTP traces endpoint). Override with:

```bash
# In shell or .env.local
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318/v1/traces
```

Or in `appsettings.json` / `appsettings.Development.json`:

```json
{
  "Otlp": {
    "Endpoint": "http://localhost:4318/v1/traces"
  }
}
```

## Services instrumented

| Service | Name in Jaeger |
|---------|---------------|
| Identity | `fairspot-identity` |
| Booking | `fairspot-booking` |
| Profile | `fairspot-profile` |
| Configuration | `fairspot-configuration` |
| Reporting | `fairspot-reporting` |
| Audit | `fairspot-audit` |
| Notification | `fairspot-notification` |
| Customer | `fairspot-customer` |

## What is NOT logged

Traces and logs carry span metadata (method, path, status, duration, error flag) plus the `tenant_id`
operator dimension. They must **not** carry secrets, bearer tokens, raw user/actor/recipient IDs,
names, emails, license plates, or full request payloads. `tenant_id` is a tenant *identifier* (an
operator filter), not tenant business data or PII.
