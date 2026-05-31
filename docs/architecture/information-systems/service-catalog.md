# Service Catalog

| Service | Capability | Owns Data | Exposes | Criticality | Status |
| --- | --- | --- | --- | --- | --- |
| Booking | Booking, Draw, allocation, cancellation, usage, penalties, and booking workflow lifecycle. | Booking requests, allocations, Draw attempts, metrics snapshots, penalties. | Employee APIs, HR/admin APIs, Draw workflow actions, events. | High | Partial |
| Identity | User context and authentication/authorization boundary. | Local identity fallback where used; otherwise external IdP owns identities. | `/me`, claim mapping, OIDC integration. | High | Partial |
| Profile | Employee profile, vehicle, default vehicle, and eligibility facts. | Profile snapshots, vehicles, eligibility facts. | Employee/HR APIs, profile events. | High | Partial |
| Configuration | Tenant parking policy, location, time slot, capacity/resource map, closures, capabilities, publication validation, and Draw schedule. | Policies, locations, slots/resources, capacity/resource-map versions, publication history. | Admin APIs, facilities APIs, policy/capacity/resource-map events. | High | Partial |
| Customer | Tenant onboarding, tenant lifecycle, readiness, support contacts, identity setup metadata. | Tenant lifecycle and setup/readiness state. | Tenant/admin APIs, readiness events. | High | Placeholder |
| Notification | Notification records, preferences, templates, delivery state. | Notification records, preferences, delivery history. | APIs, SSE/events, delivery events. | Medium/High | Partial |
| Audit | Business evidence and sensitive access traceability. | Audit records, evidence references, PII mapping, retention/integrity state. | Auditor/admin APIs, audit reference events. | High | Partial |
| DataHub | Cross-service read models, projection health, advisory impact-preview inputs, and sponsor-safe aggregate views. | Event inbox, projections, checkpoints, read stores. | Query APIs, dashboard/report views, export data, projection health. | Medium/High | Placeholder |
| Reporting | Report catalog/configuration or presentation surface over DataHub. | Report metadata only if retained. | APIs/views/exports over approved DataHub views. | Medium | Deferred |
| Web app | Browser UI for employee, HR, tenant admin, system admin, audit/report views. | Client state only. | Role-specific screens. | High | Partial |
| Mobile app | Employee self-service mobile UI. | Client state only. | Booking, My Spots, notifications, profile/default vehicle screens. | Medium/High | Partial |
| Feedback | Authenticated pilot feedback. | Feedback records if approved. | Feedback APIs/views/events. | Low/Medium | Deferred |
| Billing | Future commercial account capability. | Future commercial metadata only if approved. | Future APIs. | Low | Deferred |

## Service Ownership Rules

- Service criticality does not imply that DataHub may own command state.
- Deferred services stay visible so diagrams do not imply accidental deletion.
- In-memory repositories are not customer-ready for P0 tenant or operational state.
- Services must expose tenant-scoped APIs and events; tenant IDs cannot be caller supplied for authenticated commands.

## Visible Service Gaps

- Customer needs physical tenant persistence.
- DataHub needs event inbox, read-model store, projection health, and first query APIs.
- Configuration needs customer-ready resource-map publication APIs/events and validation evidence.
- DataHub needs HR support summary, policy-impact preview, resource-map publication summary, and sponsor management summary projections.
- Reporting needs a keep/remove/re-scope decision once DataHub is established.
- Feedback and Billing remain deferred.

## Source Evidence

- [Function Map Validation](/function-map-validation)
- [Implementation Tracker](/implementation-tracker)
