# Observability

| Signal | Purpose | Target Boundary | Status | Source Evidence |
| --- | --- | --- | --- | --- |
| Logs | Diagnose service behavior and incidents. | Operator-facing technical evidence. | Partial | [Logging and Monitoring](/security/logging-monitoring) |
| Metrics | Health, latency, rates, saturation, event lag, workflow progress, and alerting. | Operator-facing dashboards and alerts. | Partial | [Local Metrics Dashboard](/local-metrics-dashboard) |
| Traces | Request correlation across services and support handoff from audit evidence to technical diagnostics. | Technical correlation, optionally linked to audit IDs. | Partial | [Local Observability](/local-observability) |
| Audit records | Business decisions, privileged actions, sensitive access, and explainable allocation evidence. | Business-facing evidence, not raw telemetry. | Partial | [Audit](/business-layer/audit) |
| DataHub projection health | Event inbox lag, failed events, poison events, rebuild state, and projection freshness. | Operator/administrator diagnostic evidence. | Placeholder | [Data Architecture](/architecture/information-systems/data-architecture) |
| Dapr runtime health | Sidecar/component health, pub/sub delivery, workflow execution, resiliency behavior. | Operator-facing runtime evidence. | Placeholder | [Dapr-First Standards](/production/dapr-first-production-standards) |
| Resource-map publication health | Publication validation failures, effective version, closure/capability changes, and projection lag. | Facilities/admin/operator evidence. | Placeholder | [Information Systems](/architecture/information-systems/), [Business Policies](/architecture/business/policies) |
| Backup/restore evidence | Backup completion, restore drill result, recovery point, data-loss window, and defects. | Operator and architecture readiness evidence. | Placeholder | [Backup And Restore](/production/backup-restore), [RTO/RPO Requirements](/production/rto-rpo-requirements) |

## Target Rules

- Technical telemetry does not replace business audit evidence.
- Trace/correlation IDs may link technical and business evidence.
- Public hosted profiles must have enough logs/metrics/traces to diagnose incidents without exposing secrets or confidential data.
- HR/admin/auditor business screens should read Audit and approved DataHub projections, not raw Grafana/Loki/Jaeger data.
- Logs and traces must not contain bearer tokens, secrets, raw user IDs, names, emails, license plates, or full request payloads.
- OpenTelemetry is the portability boundary; local/demo/client profiles may use different backends.
- Hosted profiles need retention, backup, and access controls for telemetry appropriate to pilot/customer data.
- Restore drills and hosted smoke runs should produce attachable evidence with secrets and tokens redacted.
- Sponsor and HR business summaries must come from Audit/DataHub, not raw telemetry dashboards.

## Minimum Hosted Evidence

Before customer traffic:

- API gateway and service health are visible.
- Login, booking, Draw, notification, audit, and DataHub/read-model smoke flows produce correlated logs/traces.
- Draw workflow progress and failure states are visible without reading process memory.
- Resource-map publication and DataHub projection freshness are visible before HR/admin/sponsor dashboards rely on them.
- Event publishing/consumption lag and failed events are visible.
- Backup/restore drill evidence records recovery point, validation checks, and defects.
- Operator-only dashboards are private or protected by Cloudflare Access.
- Business audit records can be correlated to technical traces by trace/correlation ID where available.

## Visible Observability Gaps

- Hosted telemetry retention and operator access model need validation.
- DataHub projection health views are not complete.
- Dapr workflow/component health evidence is not complete.
- Backup/restore evidence and RTO/RPO proof are not complete for hosted pilot.
- Resource-map publication health and impact-preview projection health are not complete.
- Alert thresholds for customer pilot are placeholders.
