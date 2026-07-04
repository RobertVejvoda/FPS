# Monitoring

Monitoring describes how FairSpot proves that the system is healthy, fair, auditable, and ready for client operation. The monitoring model must work across local development, demo, and client-owned production.

## Telemetry Boundary

FairSpot should instrument services with OpenTelemetry-compatible metrics, logs, and traces. Deployment profiles decide where telemetry is stored and visualized:

| Profile | Default monitoring target | Purpose |
| --- | --- | --- |
| Local | Prometheus, Grafana, and local tracing such as Jaeger. | Fast developer feedback and service-level debugging. |
| Demo | Low-cost dashboards and trace/log retention sufficient for evaluation. | Prove usage, performance, error rate, notification delivery, draw duration, and audit/reporting behavior. |
| Client production | Client observability platform through OpenTelemetry Collector/exporters. | Integrate with existing client operations, alerting, SIEM, and incident processes. |

Client production examples include Dynatrace, Grafana/Prometheus, Splunk, Datadog, New Relic, DigitalOcean Monitoring, or equivalent. FairSpot should not require one vendor-specific SDK in application code.

## Required Signals

| Signal | Examples |
| --- | --- |
| Usage | active tenants, active users, booking requests, cancellations, confirmations, no-shows, admin policy changes |
| API health | request count, latency percentiles, error rate, authentication failures, rate-limit events |
| Draw processing | scheduled draw count, draw duration, eligible request count, allocated/rejected/pending counts, deterministic fallback count |
| Messaging | published events, consumer lag/backlog, dead-letter count, retry count |
| Notification | in-app events, SSE reconnects, email send attempts, delivery failures, preference suppressions |
| Audit and reporting | audit write count, audit query latency, reporting projection lag, export count |
| Infrastructure | container restarts, CPU/memory, storage growth, cache health, broker health, Dapr sidecar health |
| Security | privileged access, secret access, failed authorization, GDPR erasure requests, data export access |

## Technical Telemetry And Business Activity

Production monitoring must keep technical telemetry and business activity evidence separate:

| Evidence | Storage | Primary consumer | Production use |
| --- | --- | --- | --- |
| Technical logs | Client observability platform or local Loki equivalent. | Operators and developers. | Diagnose service errors, dependencies, retries, and request failures. |
| Metrics | Prometheus-compatible backend or client APM. | Operators, SRE, selected admins. | Dashboards, alerts, SLO evidence, trend analysis. |
| Traces | OpenTelemetry-compatible tracing backend. | Operators and developers. | Cross-service diagnosis and latency analysis. |
| Business activity | FairSpot Audit service. | Auditors, HR/facility managers, tenant admins, security reviewers. | Business accountability, compliance evidence, dispute resolution, and export. |

Business-facing audit views must not expose raw technical logs. They should query the Audit service and show stable business actions, actor hash or approved actor display, affected entity, result, reason code, and timestamp.

Technical telemetry and business activity may be linked by correlation metadata:

- `traceId` and `spanId` from the origin OpenTelemetry activity;
- `sourceEventId`, command ID, or business event ID;
- `correlationId` or workflow ID where present.

These identifiers are support links only. They do not replace tenant scoping, actor pseudonymisation, authorization, audit retention, or idempotency.

## Health Checks

All FairSpot services expose `GET /health` returning a JSON body with overall status and per-check results:

```sh
# Smoke all service health endpoints after starting the local harness
for port in 5192 5131 5197 5157 5161 5171 5141; do
  status=$(curl -s http://localhost:$port/health | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['status'])" 2>/dev/null || echo "UNREACHABLE")
  echo ":$port $status"
done
```

Expected result when all services are up: each port returns `Healthy`.

## OpenTelemetry Export (Follow-Up Gap)

FairSpot services use ASP.NET Core's built-in request logging (`ILogger`) which produces structured log output to stdout. This is visible in container logs and can be forwarded by any log shipper. However, OTLP trace and metric export requires the OpenTelemetry SDK and exporter packages to be registered in each service — this has not been added in this slice.

**What this slice delivers:** `GET /health` endpoints on all services (implemented). Structured logs to stdout (built-in).

**What a follow-up slice should add** to activate OTLP traces and metrics:

1. Add NuGet packages to SharedKernel (or each service):
   - `OpenTelemetry.Extensions.Hosting`
   - `OpenTelemetry.Instrumentation.AspNetCore`
   - `OpenTelemetry.Exporter.OpenTelemetryProtocol`

2. Register in each service `Program.cs`:
   ```csharp
   builder.Services.AddOpenTelemetry()
       .WithTracing(b => b
           .AddAspNetCoreInstrumentation()
           .AddOtlpExporter())
       .WithMetrics(b => b
           .AddAspNetCoreInstrumentation()
           .AddOtlpExporter());
   ```

3. Set env vars before starting services:
   ```sh
   export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318
   export OTEL_SERVICE_NAME=fps-booking   # one per service
   ```

Once the SDK is registered, pointing `OTEL_EXPORTER_OTLP_ENDPOINT` at a client's OpenTelemetry Collector (Dynatrace, Grafana, Splunk, Datadog, New Relic, or equivalent) requires only a config change — no application code change.

**Redaction:** configure the OTLP collector to drop `http.request.header.authorization` and similar credential-bearing attributes. See [Integration Evidence](./integration-evidence) for redaction guidance.

## Open Source Monitoring

Open source tools are the preferred local baseline and a valid client-production option when the client operates them:

### Prometheus
Prometheus is an open-source systems monitoring and alerting toolkit. It is particularly well-suited for monitoring dynamic cloud environments and microservices.

### Grafana
Grafana is an open-source platform for monitoring and observability. It allows you to query, visualize, alert on, and understand your metrics no matter where they are stored. It is often used in conjunction with Prometheus.

### Jaeger
Jaeger is an open-source, end-to-end distributed tracing tool. It is used for monitoring and troubleshooting microservices-based distributed systems.

---

## Hosted Provider Monitoring

Release 1 NAS/Cloudflare and the DigitalOcean cloud follow-up may use provider/resource monitoring for host health, network traffic, disk pressure, load balancers, and platform events. Application telemetry remains FairSpot-owned through OpenTelemetry-compatible metrics, logs, and traces. Business activity remains in the Audit service, not in provider logs.

Provider monitoring is therefore supporting evidence only. A hosted profile is acceptable when operators can see:

- service/container health and restarts;
- CPU, memory, storage, and network saturation;
- ingress/WAF and rate-limit events;
- Dapr sidecar/component health where exposed;
- backup/restore and scheduled-job outcomes;
- an OpenTelemetry path for application traces and metrics.
