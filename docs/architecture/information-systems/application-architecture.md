# Application Architecture

| Application / Component | Responsibility | Source Of Truth | Notes |
| --- | --- | --- | --- |
| Booking | Booking request lifecycle, Draw, allocation, cancellation, usage confirmation, booking events. | Booking service | Core domain and highest-complexity bounded context. |
| Identity | Authenticated user context and OIDC integration boundary. | Identity service / IdP | Tenant and user identity must come from authenticated claims. |
| Profile | Employee/profile facts, vehicle facts, eligibility, HR bootstrap/import. | Profile service | Booking validates submitted vehicle/profile facts against Profile-owned data. |
| Configuration | Parking policy, locations, slots, publication/history. | Configuration service | Booking integration with Configuration remains an important architecture boundary. |
| Customer | Tenant lifecycle, readiness, identity setup, parking bootstrap. | Customer service | Durable Customer state is a known gap. |
| Notification | In-app/email notification records, preferences, API/SSE. | Notification service | Notification failure must not roll back authoritative booking state. |
| Audit | Append-only business audit records, query, retention, integrity, PII mapping. | Audit service | Business evidence source, distinct from technical telemetry. |
| DataHub | Cross-service CQRS read models and projections. | DataHub | Target for durable event-fed operational reads. |
| Reporting | Legacy/transitional report surface and possible report catalog metadata. | Reporting service | Should not own PostgreSQL operational projections. |
| Web app | Role-centered browser experience for employee, HR, admin, reporting, and audit surfaces. | Web client | Must avoid exposing technical tenant details to non-technical roles. |
| Mobile app | Employee self-service mobile experience. | Expo mobile client | Employee-first flow for booking, notifications, profile, and My Spots. |

## Source Evidence

- [Software Architecture](/technology-layer/software-architecture)
- [Function Map Validation](/function-map-validation)
- [Application Layer](/application-layer)
