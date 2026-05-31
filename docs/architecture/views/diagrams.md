# Diagrams

This index keeps current diagrams discoverable and makes missing architecture views explicit.

## Existing Diagrams

| Diagram | Purpose | Source / Export | Status |
| --- | --- | --- | --- |
| Exchange map | Show high-level exchanges and business capability relationships. | [Exchange Map image](/images/fps-exchange-map.png) | Source evidence |
| Function map | Show function/service relationship and implementation alignment. | [Function Map image](/images/fps-function-map.png) | Source evidence |
| Application architecture | Show application/service structure. | [Application Architecture image](/images/fps-application-architecture.png) | Source evidence |
| Software architecture | Show software packages and service boundaries. | [Software Architecture](/technology-layer/software-architecture) | Source evidence |

## Required Architecture Views

| View | Layer | Status | Notes |
| --- | --- | --- | --- |
| Capability map | Business | Placeholder | Should restate business capabilities in customer-ready target form. |
| Value stream map | Business | Placeholder | Should show employee request, Draw, allocation, cancellation, and HR/admin operations. |
| Business process flow | Business | Partial | Existing BPMN/source docs exist; target process view needs consolidation. |
| Application cooperation view | Information Systems | Partial | Existing application/software diagrams are source evidence; DataHub direction must be reflected. |
| Data ownership and read-model view | Information Systems | Placeholder | Should clarify write-service ownership, events, DataHub projections, and report/read APIs. |
| API and event context view | Information Systems | Placeholder | Should show service-to-service contracts and external integration boundaries. |
| Runtime deployment view | Technology | Partial | Existing logical architecture and hosting docs exist; NAS/Cloudflare/Dapr profile needs one target view. |
| Workflow execution view | Technology | Placeholder | Should show scheduled Draw trigger, manual trigger, Dapr workflow actions, idempotency, and UI progress visibility. |
| Observability view | Technology | Placeholder | Should show logs, metrics, traces, alerting, and customer support diagnostics. |
| Trust boundary view | Security | Placeholder | Should show browser/mobile/API/service/state boundaries, Cloudflare, Dapr mTLS, secrets, and tenant data isolation. |
| Privacy and audit view | Security | Placeholder | Should show personal data, retention, audit trail, and notification responsibilities. |
| Transition roadmap view | Architecture States | Placeholder | Should show customer-ready target gaps, transition increments, and deferred billing/reporting scope. |

## Rules

- New authoritative diagrams should be indexed here.
- Source model files and exported images should both be discoverable.
- Diagrams should state which stakeholder concern they answer.
- Missing required diagrams stay listed as placeholders until replaced by authoritative diagrams.
