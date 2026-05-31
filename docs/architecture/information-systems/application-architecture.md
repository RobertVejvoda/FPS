# Application Architecture

FairSpot uses independently deployable services around business ownership boundaries. Command-side writes stay with the owning service. Cross-service reads are projected into DataHub when a single service query is not enough.

## Application Components

| Application / Component | Responsibility | Source Of Truth | Status | Notes |
| --- | --- | --- | --- | --- |
| Booking | Booking request lifecycle, Draw, allocation, cancellation, usage confirmation, penalties, booking events, and Draw workflow actions. | Booking service | Partial | Core domain and highest-complexity bounded context. Booking state is not owned by DataHub or Reporting. |
| Identity | Authenticated user context, OIDC integration boundary, current user/tenant/role resolution. | Identity service / IdP | Partial | Tenant and user identity must come from authenticated claims and fail closed when missing. |
| Profile | Employee facts, vehicle facts, default vehicle, eligibility, company-car/accessibility facts, HR bootstrap/import. | Profile service | Partial | Booking validates submitted vehicle/profile facts against Profile-owned data. |
| Configuration | Parking policy, locations, time slots, slots/resource maps, capacity versions, closures, resource capabilities, Draw schedule, publication history, and policy/capacity validation. | Configuration service | Partial | Booking integration with Configuration is a key boundary; default/stub policy paths and unpublished resource-map drafts are not customer-ready. |
| Customer | Tenant lifecycle, readiness, identity setup, support contacts, parking bootstrap, tenant object storage. | Customer service | Placeholder | Durable Customer state is a P0 gap. |
| Notification | In-app/email notification records, preferences, templates, delivery status, API/SSE. | Notification service | Partial | Notification failure must not roll back authoritative Booking state. |
| Audit | Append-only business audit records, query, retention, integrity checks, PII mapping. | Audit service | Partial | Business evidence source, distinct from technical telemetry and DataHub projections. |
| DataHub | Cross-service CQRS read models, event inbox, projections, dashboards/read APIs, approved export data, impact-preview inputs, and sponsor-safe aggregate summaries. | DataHub | Placeholder | Target for durable event-fed operational reads and customer-facing summaries; does not own commands or policy publication. |
| Reporting | Report catalog/configuration and possible presentation surface over approved DataHub views. | Reporting service | Deferred | Should not own PostgreSQL operational projections or cross-service read models. |
| Web app | Role-centered browser experience for employee, HR, tenant admin, system admin, reporting, and audit surfaces. | Web client | Partial | Employee, HR, administrator, and operator defaults must differ. |
| Mobile app | Employee self-service mobile experience. | Expo mobile client | Partial | Employee-first flow for booking, notifications, profile, default vehicle, and My Spots. |

## Boundary Rules

- A service that owns a business fact accepts commands and stores authoritative state for that fact.
- DataHub serves cross-service reads and projections; it must not accept corrective commands or mutate owning service state.
- Configuration publishes resource-map and policy versions; Booking uses published compatible versions for decisions.
- Policy-impact preview is advisory and read-model based. It must not allocate spaces, mutate policy, or bypass Configuration publication.
- HR support reads may combine Booking-owned lifecycle facts and DataHub summaries, but must not expose hidden Draw internals or unrelated employee data.
- Reporting can expose report definitions, filters, visibility rules, and exports over approved DataHub views; it must not become the read-model store.
- Audit remains the evidence source. DataHub may store audit references and safe metadata, not raw audit payloads or PII mappings.
- Web and mobile clients must not compose privileged behavior by bypassing backend authorization.
- Dapr is the integration/runtime boundary for pub/sub, workflows, state, service invocation, secrets, and mTLS where supported.

## Missing Or Incomplete Views

- Application cooperation diagram with DataHub, Audit, Notification, and Reporting separated.
- Customer persistent storage design.
- Role-specific web client application view for employee, HR, tenant administrator, and system administrator.
- Resource-map publication application view across Configuration, Booking, DataHub, Audit, and Web.
- Sponsor management summary application view over DataHub projections.
- Reporting cleanup decision if report catalog/configuration remains as a separate service.

## Source Evidence

- [Software Architecture](/technology-layer/software-architecture)
- [Function Map Validation](/function-map-validation)
- [Application Layer](/application-layer)
- [DataHub](/application-layer/datahub)
