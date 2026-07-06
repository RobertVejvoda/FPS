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
| EnvoyGatewayDown | — | — | Not implemented — Envoy admin metrics endpoint not yet configured (follow-up) |

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

`FpsHighErrorRate` counts **5xx responses only**. Unauthenticated requests return 401 (4xx) and do not contribute to this alert.

To produce 5xx responses in the local harness, send malformed or oversized JSON to a write endpoint while the service is under stress, or temporarily misconfigure the service so its downstream dependency (e.g. MongoDB) is unavailable. A practical local trigger:

1. Stop the MongoDB container: `docker compose -f code/infrastructure/docker-compose.yaml stop mongodb`
2. Send a booking submission that will fail at the persistence layer:
   ```bash
   TOKEN=$(./tools/dev-auth.sh gl-employee1)
   for i in $(seq 1 20); do
     curl -sf -X POST http://localhost:10000/bookings \
       -H "Authorization: Bearer $TOKEN" \
       -H "Content-Type: application/json" \
       -d '{"facilityId":"00000000-0000-0000-0000-000000000002","locationId":"GL-HQ","licensePlate":"EMP1001","vehicleType":"Sedan","isElectric":false,"requiresAccessibleSpot":false,"isCompanyCar":false,"plannedArrivalTime":"2099-01-01T08:00:00","plannedDepartureTime":"2099-01-01T18:00:00"}' \
       -o /dev/null -w "%{http_code}\n"
   done
   ```
3. Wait ~2 minutes. If the Booking service returns 5xx due to the DB outage, `FpsHighErrorRate` moves to **Pending** then **Firing**.
4. Restore MongoDB: `docker compose -f code/infrastructure/docker-compose.yaml start mongodb`

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

In local mode, all alerts go to the `local-only` receiver (no external notification). Keep
`code/infrastructure/alertmanager/config.yaml` local-only so development machines do not send real
operator alerts.

In NAS/hosted mode, `./tools/start-container-stack.sh --nas` renders a gitignored Alertmanager config
from `code/infrastructure/nas.env`:

- warnings route to email only;
- critical alerts route to email and Discord;
- resolved notifications are enabled for critical alerts;
- the SendGrid SMTP password and Discord webhook URL are written to gitignored runtime secret files.

Required `nas.env` values:

```env
ALERTMANAGER_EMAIL_TO=you@example.com
ALERTMANAGER_EMAIL_FROM=alerts@fairspot.net
ALERTMANAGER_SMTP_SMARTHOST=smtp.sendgrid.net:587
ALERTMANAGER_SMTP_USERNAME=apikey
ALERTMANAGER_SMTP_PASSWORD=<sendgrid-mail-send-api-key>
ALERTMANAGER_DISCORD_WEBHOOK_URL=<discord-webhook-url>
```

`ALERTMANAGER_SMTP_USERNAME=apikey` is the literal SendGrid SMTP username. Use a SendGrid API key
with Mail Send permission as the password. Prefer a separate key from the Notification service key so
alerting can be rotated independently.

The generated files live under `code/infrastructure/alertmanager/runtime/`, which is ignored by Git.
Do not paste the SendGrid key or Discord webhook into tracked YAML, issues, PRs, docs, screenshots, or
terminal output.

To render and inspect only the config shape without starting the stack:

```bash
./tools/render-alertmanager-nas-config.sh code/infrastructure/nas.env
```

To validate delivery on NAS:

1. Start the NAS stack: `./tools/start-container-stack.sh --nas --env-file code/infrastructure/nas.env --skip-e2e`
2. Confirm Alertmanager is running: `docker compose --project-directory code/infrastructure --env-file code/infrastructure/nas.env -f docker-compose.yaml -f docker-compose.services.images.yml -f docker-compose.dapr.yml -f docker-compose.nas.yml -f docker-compose.services.nas.yml ps alertmanager`
3. Trigger `FpsServiceDown` by stopping one FPS service.
4. Wait 30-45 seconds.
5. Confirm email and Discord receive the firing alert.
6. Restart the service and confirm a resolved notification arrives.

Rotate either channel by replacing the value in `nas.env`, rerunning `./tools/start-container-stack.sh --nas`,
and deleting the old SendGrid key or Discord webhook in the provider UI.

---

## OBS001/OBS002 cross-reference

- Metrics source: `docs/local-metrics-dashboard.md` — Grafana dashboard and PromQL examples
- Trace correlation: `docs/local-observability.md` — linking a firing alert to a Jaeger trace via `TraceId`
