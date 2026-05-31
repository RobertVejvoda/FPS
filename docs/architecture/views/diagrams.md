# Diagrams

This index keeps current diagrams discoverable and makes missing architecture views explicit.

## Existing Diagrams

| Diagram | Purpose | Source / Export | Status |
| --- | --- | --- | --- |
| Exchange map | Show high-level exchanges and business capability relationships. | [Exchange Map image](/images/fps-exchange-map.png) | Source evidence |
| Function map | Show function/service relationship and implementation alignment. | [Function Map image](/images/fps-function-map.png) | Source evidence |
| Application architecture | Show application/service structure. | [Application Architecture image](/images/fps-application-architecture.png) | Source evidence |
| Application Architecture 1 | Show browser/mobile, gateway, IAM, services, Dapr, stores, observability, and staged target components. | [Application Architecture 1 image](/images/fps-application-arch-1.png), [Validation](/application-arch-1-validation) | Source evidence |
| Application Architecture 2 | Additional target application view. | [Application Architecture 2 image](/images/fps-application-arch-2.png) | Source evidence |
| Data architecture | Show target data architecture shape. | [Data Architecture image](/images/fps-data-architecture.png) | Source evidence |
| Logical architecture | Show logical runtime/deployment shape. | [Logical Architecture image](/images/fps-logical-architecture.png) | Source evidence |
| Software architecture | Show software packages and service boundaries. | [Software Architecture](/technology-layer/software-architecture) | Source evidence |

## Required Architecture Views

| View | Layer | Status | Notes |
| --- | --- | --- | --- |
| Capability map | Business | Placeholder | Should restate business capabilities in customer-ready target form. |
| Value stream map | Business | Placeholder | Should show employee request, Draw, allocation, cancellation, and HR/admin operations. |
| Business process flow | Business | Partial | Existing BPMN/source docs exist; target process view needs consolidation. |
| Application cooperation view | Information Systems | Partial | Existing application/software diagrams are source evidence; DataHub direction must be reflected. Robert TODO: refresh this as an authoritative application cooperation diagram. |
| Data ownership and read-model view | Information Systems | Placeholder | Should clarify write-service ownership, events, DataHub projections, and report/read APIs. Robert TODO: confirm required first DataHub projections before diagram finalization. |
| API and event context view | Information Systems | Placeholder | Should show service-to-service contracts and external integration boundaries. |
| Runtime deployment view | Technology | Partial | Existing logical architecture and hosting docs exist; NAS/Cloudflare/Dapr profile needs one target view. |
| Workflow execution view | Technology | Placeholder | Should show scheduled Draw trigger, manual trigger, Dapr workflow actions, idempotency, and UI progress visibility. |
| Observability view | Technology | Placeholder | Should show logs, metrics, traces, alerting, and customer support diagnostics. |
| Trust boundary view | Security | Placeholder | Should show browser/mobile/API/service/state boundaries, Cloudflare, Dapr mTLS, secrets, and tenant data isolation. |
| Privacy and audit view | Security | Placeholder | Should show personal data, retention, audit trail, and notification responsibilities. |
| Transition roadmap view | Architecture States | Placeholder | Should show customer-ready target gaps, transition increments, and deferred billing/reporting scope. |

## Robert TODO Placeholders

- Robert TODO: provide or approve the authoritative application cooperation diagram for the customer-ready target.
- Robert TODO: update source diagrams where Billing/Payment are future scope, Customer durable storage is a gap, and DataHub owns read models.
- Robert TODO: decide whether the current Exchange Map and Function Map should remain target diagrams or receive status annotations.
- Robert TODO: provide/update ArchiMate diagrams after this repository migration stabilizes.

## Rules

- New authoritative diagrams should be indexed here.
- Source model files and exported images should both be discoverable.
- Diagrams should state which stakeholder concern they answer.
- Missing required diagrams stay listed as placeholders until replaced by authoritative diagrams.
