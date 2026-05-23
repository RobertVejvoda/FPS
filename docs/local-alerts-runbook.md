# Local Alert Rules Runbook

OBS003 adds Prometheus alert rules for FPS service health, error rates, latency, and infrastructure dependencies.

## Viewing current alerts

| UI | URL | Purpose |
|----|-----|---------|
| Prometheus alerts tab | http://localhost:9090/alerts | Shows all configured rules, their current state (inactive / pending / firing), and the expression that triggered them |
| Alertmanager UI | http://localhost:9093 | Shows grouped active alerts; allows silencing |

Alerts go through three states:

1. **Inactive** — condition not met.
2. **Pending** — condition met, within the `for:` grace period.
3. **Firing** — condition held for the full `for:` period; Alertmanager is notified.

---

## Defined alert rules

| Alert | Trigger | Severity | Grace period |
|-------|---------|----------|-------------|
| `FpsServiceDown` | `up == 0` for any `fps-*` job | critical | 30s |
| `FpsHighErrorRate` | >5% 5xx rate on any `fps-*` job | warning | 1m |
| `FpsHighLatency` | p95 latency >2s on any `fps-*` job | warning | 2m |
| `RabbitMQDown` | `up == 0` for rabbitmq job | critical | 30s |
| `RabbitMQHighQueueDepth` | Any queue >100 messages | warning | 2m |
| `EnvoyGatewayDown` | `up == 0` for envoy-proxy | critical | 30s |

---

## Triggering a test alert (FpsServiceDown)

1. Start the full local stack: `./tools/start-local-harness.sh`
2. Verify Prometheus is scraping all services: http://localhost:9090/targets — all `fps-*` targets should show `UP`.
3. Stop one service (e.g. Identity):
   ```bash
   pkill -f "FPS.Identity"
   ```
4. Wait 30–45 seconds, then open http://localhost:9090/alerts.
5. `FpsServiceDown` should move to **Pending** (within 30s), then **Firing**.
6. In Alertmanager (http://localhost:9093), the alert appears under `FpsServiceDown`.

**Recovery:** Restart the service via `./tools/start-local-harness.sh` or `dotnet run` in the service directory. The alert resolves within one scrape cycle (~15s) after `up` returns 1.

---

## Triggering a test alert (FpsHighErrorRate)

Send repeated requests to a protected endpoint without a token:

```bash
for i in $(seq 1 30); do
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:10000/bookings
done
```

Wait ~2 minutes. `FpsHighErrorRate` will move to **Pending** if the unauthenticated (401) share exceeds 5%. Note: 401s count as `4xx` not `5xx` — to trigger 5xx, send malformed JSON to a write endpoint or check gateway logs for upstream errors.

---

## Silencing an alert locally

In the Alertmanager UI (http://localhost:9093):
1. Click the alert.
2. Click **Silence**.
3. Set a duration (e.g. 1h for a known planned outage).
4. Click **Create**.

Silenced alerts remain in Alertmanager but do not re-notify until the silence expires.

---

## Alert routing

In local mode, all alerts go to the `local-only` receiver (no external notification). To add notifications:
- Edit `code/infrastructure/alertmanager/config.yaml`
- Add a `webhook_configs`, `email_configs`, or `slack_configs` block to the `local-only` receiver or create a new receiver and route to it.

---

## OBS001/OBS002 cross-reference

- Metrics source: `docs/local-metrics-dashboard.md` — Grafana dashboard and PromQL examples
- Trace correlation: `docs/local-observability.md` — linking a firing alert to a Jaeger trace via `TraceId`
