# Local Metrics and Operations Dashboard

OBS002 adds Prometheus metrics to all FPS .NET services and provisions a Grafana operations dashboard for local smoke testing and development.

## Dashboard URL

**http://localhost:3001**

Default credentials: `admin` / `admin` (local only — change immediately in any shared environment).

The **FairSpot Development Operations** dashboard auto-provisions on first Grafana start.

The local host port defaults to `3001` so Docsify can use `3000`. Grafana still
listens on `3000` inside the Docker network. If host port `3001` is already in
use, run the local stack with a different host-side Grafana port:

```bash
FPS_GRAFANA_HOST_PORT=3002 ./tools/start-container-stack.sh --seed
```

For repeatable local use, put `FPS_GRAFANA_HOST_PORT=3002` in the ignored
`code/infrastructure/local-docker.env` file and open
`http://localhost:3002`.

---

## Platform note

The host harness uses `host.docker.internal`; Linux gets the host-gateway mapping
from Compose. The full Local-container and NAS profiles override that config
with `prometheus.containers.yaml` and scrape by Docker DNS (for example,
`fairspot-datahub:5211`). NAS therefore needs no published service ports.

## Starting the stack

```bash
docker compose -f code/infrastructure/docker-compose.yaml up -d prometheus grafana rabbitmq
./tools/start-local-harness.sh
```

---

## Dashboard panels

| Panel | Metric | What it shows |
|-------|--------|--------------|
| HTTP Request Rate | `http_requests_received_total` | Requests per second per service |
| HTTP Error Rate | 4xx+5xx / total | Fraction of requests that errored, per service |
| HTTP Duration p50/p95 | `http_request_duration_seconds` | Latency percentiles in ms per service |
| HTTP In-Flight | `http_requests_in_progress` | Concurrent active requests per service |
| GC Heap Size | `system_runtime_gc_heap_size` | .NET managed heap in MB |
| Thread Pool Queue | `system_runtime_threadpool_queue_length` | Work-item backlog in the thread pool |
| Service Health | `max by (job) (up{job=~"fairspot-(identity\|booking\|notification\|profile\|audit\|reporting\|configuration\|customer\|datahub)"})` | UP/DOWN per FairSpot service; grouping avoids stale duplicate target series after a config change |
| RabbitMQ Published Rate | `rabbitmq_channel_messages_published_total` | Events published per second |
| RabbitMQ Queue Depth | `rabbitmq_queue_messages` | Messages waiting per queue |

---

## Service scrape targets

Each FPS service exposes `GET /metrics` (prometheus-net). Prometheus scrapes every 15 seconds.

| Service | Port | Metric endpoint |
|---------|------|----------------|
| Identity | 5192 | http://localhost:5192/metrics |
| Booking | 5131 | http://localhost:5131/metrics |
| Notification | 5157 | http://localhost:5157/metrics |
| Profile | 5197 | http://localhost:5197/metrics |
| Audit | 5161 | http://localhost:5161/metrics |
| Reporting | 5171 | http://localhost:5171/metrics |
| Configuration | 5141 | http://localhost:5141/metrics |
| Customer | 5181 | http://localhost:5181/metrics |
| DataHub | 5211 | http://localhost:5211/metrics |

RabbitMQ (port 15692) is scraped from within the Docker network — the prometheus plugin is enabled via the compose command. Keycloak `start-dev` mode does not expose a `/metrics` endpoint and is not scraped.

---

## Finding a failing service

1. Open Grafana → **FairSpot Development Operations** → **Service Health** panel.
2. Red = Prometheus cannot scrape `GET /metrics` — service is down or not started.
3. Drill into **HTTP Error Rate** — spikes identify which service is returning errors.
4. Cross-correlate with Jaeger traces at **http://localhost:16686** using the `TraceId` from service logs.

---

## Metrics available per service

**HTTP (auto-instrumented by prometheus-net.AspNetCore):**
- `http_requests_received_total{method,route,code}` — counter
- `http_request_duration_seconds{method,route,code}` — histogram
- `http_requests_in_progress{method,route}` — gauge

**Runtime (prometheus-net.SystemMetrics):**
- `system_runtime_gc_heap_size` — managed heap in MB
- `system_runtime_dotnet_gc_collections` — GC collections
- `system_runtime_dotnet_thread_pool_thread_count` — thread pool threads
- `system_runtime_threadpool_queue_length` — work-item backlog
- `process_cpu_seconds_total` — CPU time
- `process_working_set_bytes` — RSS memory

## Filtering metrics by tenant

Metrics are **intentionally not labelled with `tenant_id`** (PLAT005B). The HTTP metrics are already
`{method, route, code}`; adding a per-tenant label would multiply every series by the tenant count and
risk a cardinality explosion, and the logging/monitoring contract forbids raw tenant IDs as metric
labels. So the tenant dimension lives in **logs and traces**, not metrics:

- **Per-tenant activity / errors:** filter the `FPS.Request` logs in Loki/Grafana by `tenant_id` (e.g.
  `{job="fairspot-booking"} |= "tenant_id=greenlogistics"`), or search Jaeger traces by the `tenant_id` span tag.
- **Fleet health (all tenants):** use the tenant-agnostic metrics above — they answer "is the service
  healthy / fast / erroring" without needing a tenant label.
- If a genuinely per-tenant *metric* is ever needed, use a **low-cardinality** proxy label such as
  `tenantKind` (Sandbox/Production/Evaluation), never the raw tenant id.

---

## Prometheus UI

Raw metric browser and PromQL REPL: **http://localhost:9090**

Useful queries:
```promql
# Local FairSpot service up/down targets
max by (job) (up{job=~"fairspot-(identity|booking|notification|profile|audit|reporting|configuration|customer|datahub)"})

# 95th-percentile latency for all FPS services (ms)
histogram_quantile(0.95, sum by (job, le) (rate(http_request_duration_seconds_bucket[5m]))) * 1000

# Error rate per service
sum by (job) (rate(http_requests_received_total{code=~"4..|5.."}[5m]))
  / sum by (job) (rate(http_requests_received_total[5m]))
```
