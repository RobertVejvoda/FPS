# Diagrams

This index keeps current diagrams discoverable and makes missing architecture views explicit.

The repository has two diagram classes:

- **Source evidence** - existing exports, ArchiMate models, draw.io files, BPMN files, and validation diagrams that explain prior design intent.
- **Authoritative target views** - refreshed diagrams that match the current architecture repository and can be used in customer or implementation review.

Until a target view is explicitly marked authoritative, the text architecture pages remain the source of truth where they differ from an old diagram.

## Source Evidence Inventory

| Evidence | Purpose | Source / Export | Refresh Disposition |
| --- | --- | --- | --- |
| Exchange map | Shows high-level business exchanges and capability relationships. | [Exchange Map image](/images/fps-exchange-map.png), [Architecture Views](/architecture-views) | Reuse as source evidence; refresh or annotate for customer-ready target scope and deferred Billing. |
| Function map | Shows function/service relationship and implementation alignment. | [Function Map image](/images/fps-function-map.png), [Function Map Validation](/function-map-validation) | Reuse as source evidence; supersede with target capability and application cooperation views. |
| Application architecture | Shows application/service structure. | [Application Architecture image](/images/fps-application-architecture.png) | Reuse as source evidence; replace with target application cooperation view. |
| Application Architecture 1 | Shows browser/mobile, gateway, IAM, services, Dapr, stores, observability, and staged target components. | [Application Architecture 1 image](/images/fps-application-arch-1.png), [Validation](/application-arch-1-validation) | Strong source evidence; refresh to include DataHub, Customer persistence gap, Reporting obsolescence, and hosted profile. |
| Application Architecture 2 | Additional target application view. | [Application Architecture 2 image](/images/fps-application-arch-2.png) | Source evidence only until reconciled with the current application architecture. |
| Data architecture | Shows target data architecture shape. | [Data Architecture image](/images/fps-data-architecture.png) | Source evidence; replace with DataHub/read-model ownership view. |
| Logical architecture | Shows logical runtime/deployment shape. | [Logical Architecture image](/images/fps-logical-architecture.png) | Source evidence; replace with runtime deployment and trust-boundary views. |
| Provider examples | Show older Azure/AWS variants. | [Azure Application Architecture](/images/fps-application-arch-azure.png), [Azure Logical Architecture](/images/fps-logical-architecture-azure.png), [AWS Logical Architecture](/images/fps-logical-architecture-aws.png) | Keep as environment examples only; not core target architecture unless a decision records provider selection. |
| Software architecture and packages | Shows software packages and service boundaries. | [Software Architecture](/technology-layer/software-architecture), package and service images under `docs/images/` | Source evidence; service catalog and target diagrams are authoritative after refresh. |
| BPMN process evidence | Shows older process flows. | [Draw BPMN](/process/draw.bpmn), [Subscribe Tenant BPMN](/process/subscribe-tenant.bpmn), [Generate Invoice BPMN](/process/generate-invoice.bpmn) | Draw can inform target process/workflow views; Billing invoice process is deferred. |
| Model files | Contain editable model sources. | `docs/archi/fps.archimate`, `docs/fps.drawio`, `docs/fps-composition.drawio`, `docs/wireframes.drawio` | Source model files; update only when Robert refreshes or approves the target model. |

## Target View Catalog

| Target View | Viewpoint | Owning Artifact | Current Status | Source Evidence | Refresh Decision / TODO |
| --- | --- | --- | --- | --- | --- |
| Product outcome map | Motivation and outcome | [Architecture Vision](/architecture/architecture-vision) | Optional | [Strategy](/strategy), [Roadmap](/roadmap) | Text currently sufficient; add only if customer review needs a visual summary. |
| Capability map | Capability | [Capabilities](/architecture/business/capabilities) | Placeholder | [Exchange Map image](/images/fps-exchange-map.png), [Function Map image](/images/fps-function-map.png) | Robert TODO: create target capability map with customer-first parking scope, deferred Billing, and future resource domains marked as future scope. |
| Value stream map | Value stream | [Value Streams](/architecture/business/value-streams) | Placeholder | [Exchange Map image](/images/fps-exchange-map.png), [Business Processes](/architecture/business/business-processes) | Robert TODO: show request, Draw, allocation, cancellation/reallocation, HR operations, admin policy/resource setup, and feedback. |
| Draw and cancellation process view | Business process | [Business Processes](/architecture/business/business-processes) | Partial | [Draw BPMN](/process/draw.bpmn), [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Allocation Rules](/business-layer/allocation-rules) | Consolidate into target process view; keep same-day request behavior and employee/HR/admin responsibilities visible. |
| Role interaction view | Role and actor | [Actors and Roles](/architecture/business/actors-roles) | Placeholder | [Role Intent Roadmap](/business-layer/role-intent-roadmap), [My Spots UX](/business-layer/my-spots-ux) | Create only if role-specific UI/default-view behavior needs visual validation. |
| Application cooperation view | Application cooperation | [Application Architecture](/architecture/information-systems/application-architecture) | Partial | [Application Architecture 1 image](/images/fps-application-arch-1.png), [Application Architecture image](/images/fps-application-architecture.png), [Software Architecture](/technology-layer/software-architecture) | Robert TODO: refresh as authoritative target view with Booking, Configuration, Customer, Notification, Audit, DataHub, frontend apps, Dapr, and deferred/obsolete Reporting boundary. |
| Data ownership and read-model view | Data and read-model | [Data Architecture](/architecture/information-systems/data-architecture) | Placeholder | [Data Architecture image](/images/fps-data-architecture.png), [DataHub](/application-layer/datahub) | Robert TODO: confirm first DataHub projections; show write ownership, events, read APIs, report catalog, retention/privacy constraints, and Customer physical storage gap. |
| API and event context view | API and event context | [API Contracts](/architecture/information-systems/api-contracts), [Integrations and Events](/architecture/information-systems/integrations-events) | Placeholder | Booking API/event contract pages under `business-layer/` | Add when generated contract evidence or integration review needs a visual boundary. |
| Runtime deployment view | Runtime deployment | [Deployment Profiles](/architecture/technology/deployment-profiles), [Runtime Platform](/architecture/technology/runtime-platform) | Partial | [Logical Architecture image](/images/fps-logical-architecture.png), production runbooks | Robert TODO: create target hosted profile view for NAS, Cloudflare/WAF, containers, Dapr sidecars/components, state stores, backups, and smoke evidence. |
| Draw workflow execution view | Workflow execution | [Runtime Platform](/architecture/technology/runtime-platform), [Business Processes](/architecture/business/business-processes) | Placeholder | [Draw Scheduling and Workflow](/production/draw-scheduling-and-workflow), [Draw BPMN](/process/draw.bpmn) | Robert TODO: show cron/manual trigger, single execution safety across replicas, Dapr Workflow actions, idempotency, progress read model, and next Draw schedule visibility. |
| Operations and observability view | Operations and observability | [Observability](/architecture/technology/observability) | Placeholder | [Monitoring](/production/monitoring), [Hosted Smoke Runbook](/production/hosted-smoke-runbook) | Add before hosted pilot hardening; include logs, metrics, traces, alerts, smoke tests, support diagnostics, and customer-service boundary. |
| Trust boundary view | Trust boundary | [Security Architecture](/architecture/security/security-architecture), [Controls](/architecture/security/controls) | Placeholder | [Security Model](/security/security-model), [Cloudflare WAF Profile](/security/cloudflare-waf-profile), [Logical Architecture image](/images/fps-logical-architecture.png) | Robert TODO: show browser/mobile, Cloudflare/WAF, API, services, Dapr mTLS, secrets, state stores, backups, and tenant data isolation. |
| Privacy and audit view | Privacy and audit | [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls) | Placeholder | [Data Privacy](/security/data-privacy), [Audit Capability](/business-layer/audit), [Audit Application](/application-layer/audit) | Robert TODO: show personal data, audit trail, notification duties, retention/erasure, role-safe DataHub reads, and support access limits. |
| Transition roadmap view | Transition roadmap | [Transition Architectures](/architecture/architecture-states/transition-architectures), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Placeholder | [Roadmap](/roadmap), [Implementation Tracker](/implementation-tracker) | Robert TODO: show customer-ready work packages, named transition states, delivery gates, deferred Billing, and obsolete Reporting/PostgreSQL direction. |

## Diagram Refresh Order

| Priority | Diagram | Why It Comes Next | Blocks / Supports |
| --- | --- | --- | --- |
| 1 | Application cooperation view | It is needed to explain service boundaries after the DataHub decision and Reporting cleanup. | Implementation slicing, customer technical review. |
| 2 | Data ownership and read-model view | It makes Customer durable storage and DataHub projection gaps visible before implementation. | DataHub issues, privacy review, read API design. |
| 3 | Runtime deployment and trust boundary views | They support hosted NAS/Cloudflare/WAF security review and public-domain readiness. | Hosted pilot readiness, security review. |
| 4 | Draw workflow execution view | It explains scheduled/manual Draw, Dapr Workflow, idempotency, and UI progress. | Draw workflow implementation and HR/customer clarity. |
| 5 | Capability, value stream, and process views | They support customer-facing business review once the technical target is stable. | Customer validation, business architecture approval. |
| 6 | Privacy/audit and observability views | They explain customer-service, DPO, audit, and operational support responsibilities. | Hosted operations, support model, privacy readiness. |
| 7 | Transition roadmap view | It links gaps, work package groups, named architecture versions, and deferred scope. | Architecture roadmap and release planning. |

## Robert TODO Placeholders

- Robert TODO: provide or approve the authoritative application cooperation diagram for the customer-ready target.
- Robert TODO: update source diagrams where Billing/Payment are future scope, Customer durable storage is a gap, Reporting/PostgreSQL is obsolete, and DataHub owns read models.
- Robert TODO: decide whether the current Exchange Map and Function Map should remain target diagrams or receive status annotations.
- Robert TODO: provide/update ArchiMate diagrams after this repository migration stabilizes.

## Rules

- New authoritative diagrams should be indexed here.
- Source model files and exported images should both be discoverable.
- Diagrams should state which stakeholder concern they answer.
- Missing required diagrams stay listed as placeholders until replaced by authoritative diagrams.
- Diagrams must use the current target architecture terminology: Draw, DataHub, Customer durable storage, hosted NAS/Cloudflare profile, Dapr-first runtime, and deferred Billing.
- Provider-specific diagrams are examples unless a provider decision is recorded in [Versions and Decisions](/versions-and-decisions).
