# Observability

| Signal | Purpose | Target Boundary | Source Evidence |
| --- | --- | --- | --- |
| Logs | Diagnose service behavior and incidents. | Operator-facing technical evidence. | [Logging and Monitoring](/security/logging-monitoring) |
| Metrics | Health, latency, rates, and alerting. | Operator-facing dashboards and alerts. | [Local Metrics Dashboard](/local-metrics-dashboard) |
| Traces | Request correlation across services. | Technical correlation, optionally linked to audit IDs. | [Local Observability](/local-observability) |
| Audit records | Business decisions and sensitive actions. | Business-facing evidence, not raw telemetry. | [Audit](/business-layer/audit) |

## Target Rules

- Technical telemetry does not replace business audit evidence.
- Trace/correlation IDs may link technical and business evidence.
- Public hosted profiles must have enough logs/metrics/traces to diagnose incidents without exposing secrets or confidential data.
