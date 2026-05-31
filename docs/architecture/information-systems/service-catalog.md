# Service Catalog

| Service | Capability | Owns Data | Exposes | Criticality |
| --- | --- | --- | --- | --- |
| Booking | Booking and Draw lifecycle | Booking requests, allocations, metrics, penalties | APIs, events, workflow actions | High |
| Identity | User context and auth boundary | Local identity fallback where used | `/me`, OIDC integration | High |
| Profile | Employee facts and vehicles | Profile snapshots, vehicles, eligibility | APIs/events | High |
| Configuration | Policy and capacity | Policies, locations, slots, history | APIs/events | High |
| Customer | Tenant onboarding/readiness | Tenant lifecycle and setup state | APIs | High |
| Notification | Notifications | Notification records, preferences | APIs/SSE/events | Medium |
| Audit | Business evidence | Audit records, PII mapping, retention/integrity state | APIs/events | High |
| DataHub | Read models | Projections, checkpoints, read stores | Query APIs | Medium/High |
| Reporting | Legacy report surface/catalog | Report metadata or transitional read data | APIs/views | Medium |

## Source Evidence

- [Function Map Validation](/function-map-validation)
- [Implementation Tracker](/implementation-tracker)
