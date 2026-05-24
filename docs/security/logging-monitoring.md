# Logging And Monitoring

FairSpot uses two different evidence streams that must not be mixed:

| Stream | System of record | Audience | Purpose |
| --- | --- | --- | --- |
| Technical telemetry | Logs, metrics, and traces in the selected observability stack. Local default: Grafana, Loki, Prometheus, and Jaeger. | Developers, operators, support engineers. | Diagnose failures, latency, retries, integration errors, and service health. |
| Business activity | Append-only Audit service records and product-facing audit APIs/UI. | Auditors, HR/facility managers, tenant admins, security reviewers. | Answer who did what, when, to which business object, why, and with what result. |

Technical telemetry is operational evidence. Business activity is product and compliance evidence. A business-facing screen must not be built by exposing raw Loki logs.

## Technical Logs

Technical logs are structured application and platform logs written to stdout and shipped to the deployment profile's log backend. In local development OBS004 uses Promtail and Loki so Grafana can query `logs/local-harness/*.log`.

Technical logs should include:

- stable message names suitable for filtering;
- service name, environment, endpoint, action, dependency, result, status code, duration, and safe reason code where useful;
- `TraceId` and `SpanId` from the current request or consumer span;
- source event ID, business event ID, or command ID when that identifier is non-sensitive and useful for correlation;
- exception category and safe failure classification, not raw provider payloads or stack traces unless the deployment's security policy permits them for operators.

Technical logs must not include:

- bearer tokens, refresh tokens, passwords, client secrets, connection strings, private keys, or secret-store values;
- raw names, emails, license plates, phone numbers, local-account credential verifiers, or provider credential material;
- raw user IDs, recipient IDs, actor IDs, requestor IDs, or employee IDs unless a documented deployment-specific policy explicitly allows them;
- hidden Draw internals such as lottery weights, complete candidate order, fairness diagnostics, or other employees' allocation details;
- full request or response payloads.

When a user identifier is needed for operational diagnosis, prefer a one-way pseudonymised value such as `actorHash`, `requestorHash`, or a dedicated support-safe correlation key. Do not reuse unhashed IdP subjects in logs.

## Metrics

Metrics are quantitative operational signals. They are the source for dashboards and alerts, not for business accountability.

Required metric categories:

- request count, latency percentiles, 4xx/5xx rate, and authentication/authorization failures;
- service/container/Dapr sidecar health;
- message publish/consume count, retries, dead letters, and consumer lag where available;
- booking submission, rejection, draw, allocation, cancellation, no-show, and usage-confirmation counts as aggregate counters;
- notification delivery attempts, failures, suppressions, and SSE reconnects;
- audit write count, query latency, retention runs, integrity verification, and export count.

Metrics labels must be low-cardinality and safe. Use labels such as `service`, `environment`, `tenantKind`, `eventType`, `result`, and `reasonCode`. Avoid raw tenant IDs, raw user IDs, booking IDs, license plates, emails, or unique request IDs as metric labels.

## Traces

Traces show the technical execution path across services. The selected tracing backend is deployment-specific; local development uses Jaeger.

Trace data should include:

- service and operation names;
- HTTP method, route template, status code, duration, and error flag;
- Dapr invocation, pub/sub, state, and dependency spans where available;
- safe correlation attributes such as `sourceEventId`, `businessEventId`, or `auditRecordId` where cardinality and retention are acceptable.

Trace attributes must follow the same redaction rules as logs. Authorization headers and credential-bearing attributes must be dropped by application instrumentation or the OpenTelemetry collector.

## Business Activity Records

Business activity records are append-only Audit service records created from command outcomes or domain events. They are not textual log lines and should not be scraped from technical logs.

A business activity record answers:

- who acted, represented as `actorHash` plus `actorType`;
- what action occurred, represented by a stable action name such as `booking.requestSubmitted`, `booking.requestRejected`, `booking.slotAllocated`, `configuration.policyPublished`, `audit.piiMappingErased`, or `profile.vehicleUpdated`;
- which tenant and business entity were affected;
- when the business action occurred and when it was recorded;
- what result was produced, such as `accepted`, `rejected`, `allocated`, `cancelled`, `updated`, `failed`, or `suppressed`;
- why the result happened when a safe reason code exists;
- which source event, command, or workflow caused the record;
- which technical trace can help an operator diagnose the same flow.

Minimum business activity metadata:

| Field | Purpose |
| --- | --- |
| `auditRecordId` | Stable Audit service record ID. |
| `tenantId` | Tenant boundary for authorization and filtering. |
| `action` | Stable business action name. |
| `entityType` / `entityId` | Business object affected by the action. |
| `actorType` | `employee`, `hr`, `admin`, `auditor`, `system`, or `integration`. |
| `actorHash` | SHA-256 hash of the authenticated actor ID when present. |
| `occurredAt` | Business timestamp from the command/event. |
| `recordedAt` | Audit ingestion timestamp. |
| `result` | Safe outcome classification. |
| `reasonCode` | Safe reason code when applicable. |
| `sourceEventId` | Domain event ID or command ID used for idempotency. |
| `correlationId` | Request/workflow correlation ID when present. |
| `traceId` | Origin technical trace ID when present. |
| `spanId` | Origin span ID when useful. |
| `processingTraceId` | Optional consumer-side trace ID when an async consumer processes the event in a different trace. |

The Audit service may expose role-specific business summaries to auditors, HR, and admins. Those summaries must be generated from audit records and authorization rules, not from Loki queries.

## Trace Correlation

Trace correlation links the two streams without making either stream the source of truth for the other.

For synchronous commands:

1. The API request enters FairSpot and OpenTelemetry creates or continues the current `Activity`.
2. The command handler performs the business action.
3. The domain event or audit command includes `TraceId = Activity.Current?.TraceId.ToString()` and, where useful, `SpanId = Activity.Current?.SpanId.ToString()`.
4. The Audit service stores those values as optional metadata.

For asynchronous consumers:

1. The producing service includes the origin `traceId`, `spanId`, `correlationId`, and `sourceEventId` in the domain event envelope.
2. The consuming service starts or continues its own processing span.
3. The audit record stores the origin `traceId` and may also store `processingTraceId` for the consumer-side work.

Rules:

- `traceId` is correlation metadata only. It must never replace tenant, actor, action, entity, timestamp, result, reason, or idempotency fields.
- Audit must still work when `traceId` is null.
- A trace may be short-lived or deleted before audit retention expires.
- Business audit retention is controlled by Audit retention policy; telemetry retention is controlled by the observability backend.

## Access Model

| Evidence | Normal access |
| --- | --- |
| Grafana/Loki technical logs | Operators and developers with operational responsibility. |
| Grafana/Prometheus metrics and alerts | Operators, developers, and selected tenant admins for non-sensitive service health views. |
| Jaeger traces | Operators and developers with operational responsibility. |
| Audit business activity API/UI | Tenant-scoped `auditor`, `admin`, and selected HR/facility roles based on the action category. |
| PII mapping for actor resolution | Separately approved privacy/security path only. |

Auditor, HR, and admin product screens should show business-readable actions and outcomes. They should not expose raw exception text, stack traces, infrastructure topology, secrets, or unrelated employees' private data.

## Local Open-Source Stack

The local default stack is:

- **Prometheus** for metrics;
- **Grafana** for dashboards and Explore;
- **Loki** for technical log aggregation;
- **Promtail or Alloy** for local log shipping;
- **Jaeger** for distributed traces.

Local Grafana panels may link from logs to traces by `TraceId`. Product audit screens should link in the opposite direction only when the viewer is authorized: from business activity record to trace ID or support evidence, without exposing raw technical logs by default.
