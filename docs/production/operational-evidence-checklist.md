# OPS009 Operational Evidence Checklist

**Status:** Provider-neutral operational evidence package for observability, backup, restore, and incident readiness — the minimum a self-hosted or BYOC operator captures before FairSpot is shown as production-handoff capable.
**Scope:** Public open-core, provider-neutral. Hosted-operator private artifacts, environment values, and executable runbooks stay in the private `fairspot-platform` repository ([Open-Core Documentation Boundary](../strategy-layer/open-core-boundary), #684).

This page consolidates the operational **contracts** already defined per topic into one runnable checklist and evidence shape. It does not restate them — it links to [Monitoring](./monitoring), [Backup and Restore](./backup-restore), [RTO/RPO Requirements](./rto-rpo-requirements), [Availability Model](./availability-model), [Incident Handling](./incident-handling), and [Maintenance](./maintenance), and is applied against a profile from the [Deployment Profile Template](./deployment-profile-template).

Observability requirements stay OpenTelemetry-compatible and provider-neutral; nothing here builds a managed observability platform, per-cloud dashboards, or SLA/SLO commitments.

---

## How to use

1. Pick the target profile (Local, NAS/Cloudflare, DigitalOcean, Client-owned/BYOC) from the [Deployment Profile Template](./deployment-profile-template).
2. Walk Sections 1–2 to confirm observability and backup/restore readiness.
3. Run a restore drill and record it with Section 3's evidence shape.
4. Run Section 4's post-restore smoke checks; attach results to the drill record.
5. Keep Section 5's incident/rollback evidence ready. Real customer data must not be processed until backup **and** restore evidence has been captured for the selected profile.

---

## 1. Observability readiness

- [ ] Services emit **OpenTelemetry-compatible** metrics, logs, and traces; no vendor-specific SDK in application code. The profile selects where signals land (Local: Prometheus/Grafana/Jaeger; Client: their platform via an OTel Collector).
- [ ] Required signal categories are visible: **usage, API health, Draw processing, messaging, notification, audit & reporting, infrastructure (including Dapr sidecar health), and security**.
- [ ] **Correlation identifiers** are present and searchable: `traceId`/`spanId`, `sourceEventId`/command/business-event ID, and `correlationId`/workflow ID. These support links only — they never replace tenant scoping.
- [ ] **`tenant_id`** is an operator dimension on logs and as an OTel span attribute, sourced only from the validated JWT tenant claim / Dapr event envelope (never from body, query, or header); platform/no-tenant requests use the `__none__` sentinel.
- [ ] Health endpoints respond: `GET /health` on every service returns `{ status: "Healthy", checks[] }`.
- [ ] Dashboards exist for service health and per-tenant activity (e.g. Grafana "FPS Local Operations"; Prometheus `up{job=~"fps-..."}`).
- [ ] Baseline **alert rules** are wired: `FpsServiceDown`, `FpsHighErrorRate` (>5% 5xx), `FpsHighLatency` (p95 >2s), `RabbitMQDown`, `RabbitMQHighQueueDepth`. Alert routing is configured for the profile.
- [ ] Business activity is read from the **Audit service**, not from raw technical logs; audit views never expose raw logs, secrets, tokens, or personal identifiers.

> **OTLP export status:** OpenTelemetry tracing is wired to local Jaeger (OBS001, [Local Observability](../local-observability)). Broader OTLP metric export and the client-collector handoff path remain a documented follow-up ([Monitoring → OpenTelemetry Export](./monitoring)); record it as a known gap on non-local profiles until closed.

---

## 2. Backup & restore readiness

- [ ] **Scope** covered: service state stores, read models, configuration, identity mappings, object storage, and deployment metadata.
- [ ] **Tenant safety:** a restore preserves tenant boundaries — restore only the affected tenant scope, or restore to a temporary environment and copy back only approved data.
- [ ] **Encryption:** backup artifacts are encrypted at rest and classified Confidential/Secret; secrets stay out of Git and logs.
- [ ] **RTO/RPO** targets for each capability are known and achievable for the profile ([RTO/RPO Requirements](./rto-rpo-requirements)) — e.g. Booking write model 1h/15min, Audit 1h/near-zero, read models rebuildable.
- [ ] A restore drill has been **rehearsed at least once** for the profile, recorded with Section 3.
- [ ] Recovery keeps commands/consumers **idempotent** and tenant isolation intact during failover/restore; manual recovery is treated as an auditable action.

Provider commands, schedules, credentials, storage locations, and the operator restore-drill log are private ([Backup and Restore](./backup-restore) → private runbook).

---

## 3. Restore-drill evidence record

Capture one record per restore drill. Keep it provider-neutral — no secrets, real environment values, or recovery keys.

| Field | Value |
| --- | --- |
| Drill date/time (UTC) | |
| Operator (actor) | |
| Profile | Local / NAS-Cloudflare / DigitalOcean / Client-owned |
| Recovery point (backup timestamp restored) | |
| Affected tenants | |
| Affected services / stores | |
| RTO result (target → actual) | |
| RPO result (target → actual) | |
| Post-restore smoke result | N pass / N fail (attach Section 4) |
| Defects found | |
| Follow-up issue links | #… |
| Sign-off (operator / reviewer) | ☐ restore ok / ☐ evidence accepted |

Consistent with [Release Evidence Template](./release-evidence-template); the executable drill procedure and captured operator evidence live in the private runbook.

---

## 4. Post-restore smoke checks

Run through the profile's API gateway after a restore (routes shown are the logical service routes; the gateway fronts them under one HTTPS origin). Record pass/fail against the drill record.

| Check | Endpoint | Expected |
| --- | --- | --- |
| Service health | `GET /health` (each service) | `status: "Healthy"` |
| Tenant readiness | `GET /tenants/{tenantId}/readiness` | `isReady: true` (dependent-service checks pass) |
| Login / tenant context | `GET /me` | `{ userId, tenantId, roles }` for the expected tenant |
| Profile snapshot | `GET /profile/snapshot` | `parkingEligible` resolves |
| Booking read | `GET /bookings` | prior requests present (recovery point intact) |
| Booking write | `POST /bookings` | accepted → `status: Pending` |
| Notification consumption | `GET /notifications`, `GET /notifications/unread-count` | records/count return; booking event was consumed |
| Audit ingestion / query | `GET /audit` | events queryable (booking/security events ingested) |
| Reporting projection | `GET /reports/parking/summary` | summary rebuilds/returns |
| DataHub projection health | `GET /datahub/projection-health` | projections healthy / caught up |

- [ ] All health/readiness checks green before functional checks.
- [ ] Tenant context on every response matches the restored tenant (no cross-tenant leakage).
- [ ] Event-driven observers (Notification, Audit, Reporting, DataHub) have caught up to the recovery point.

---

## 5. Incident & rollback evidence

- [ ] **Classification** recorded by customer impact, data-protection risk, availability, integrity, and security scope ([Incident Handling](./incident-handling)).
- [ ] **Evidence preserved:** relevant audit records, technical logs, traces, metrics, deployed image/commit versions, and user-impact notes.
- [ ] **Rollback** evidence captured: previous known-good image tag, rollback command/path for the profile, and the data-volume handling decision.
- [ ] **Communication:** owner, path, and follow-up summary documented.
- [ ] **Maintenance changes** carry relevant CI, smoke, backup/restore, and rollback evidence before traffic is considered healthy ([Maintenance](./maintenance)).
- [ ] Follow-up feeds architecture change control, security gaps, waivers, and new slices.

Provider dashboards, escalation paths, contacts, and operational commands are private (per the moved-private runbooks, #684).

---

## Validation evidence

- Markdown reviewed for internal consistency; `git diff --check` clean.
- Facts sourced from current public docs: [Monitoring](./monitoring), [Local Observability](../local-observability), [Backup and Restore](./backup-restore), [RTO/RPO Requirements](./rto-rpo-requirements), [Availability Model](./availability-model), [Incident Handling](./incident-handling), [Maintenance](./maintenance), [Hosted Readiness Expectations](./hosted-smoke-runbook), and the health/smoke routes verified in the service controllers and [Local Test Harness](./local-test-harness).
- No private operator steps, secrets, real environment values, recovery keys, customer evidence, managed-platform build, or SLA/SLO commitments added.
