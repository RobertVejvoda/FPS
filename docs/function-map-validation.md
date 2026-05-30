# Function Map Validation

This note validates the exported function-map asset against current docs and code.

Design asset checked: [docs/images/fps-function-map.png](./images/fps-function-map.png)

Raw source: [docs/fps.drawio](./fps.drawio), diagram `function-map`

## Summary

The function map is a target capability map, not a delivery-status map. Its current strongest implemented areas are:

`Web/Mobile -> Identity/API gateway -> Booking/Profile/Configuration/Notification/Audit/Reporting -> Dapr/state/pubsub/observability`

The map is broadly directionally correct, but several boxes are either not current app priorities or are implemented under different boundaries. The important Customer gap is Customer Service persistence: tenant onboarding/readiness foundations exist, but they are backed by in-memory repositories. Reporting also needs durable persistence; unlike most service state, Reporting is a good fit for PostgreSQL because its workload is fixed report queries, grouping, date ranges, totals, and exports. Billing and Payment Gateway are valid target/reference capabilities but not a priority for making the app work. Feedback is not implemented, but a small authenticated feedback path would be reasonable for testing and customer evaluation.

## Function Validation

| Function-map element | Diagram says | Docs say | Code status | Validation |
| --- | --- | --- | --- | --- |
| Web App | Channel. | Web app is a primary user interface for employee, HR, admin, reporting, audit, profile, and notifications. | React app exists under `code/web/fps-web`; routes cover My Spots/bookings, HR Operations, Tenant Admin, Configuration, Reporting, Audit, Profile, Notifications. | Implemented for current baseline. |
| Mobile App | Channel. | Mobile app is employee-facing for login, My Spots/bookings, notifications, profile, and booking actions. | Expo app exists under `code/mobile/fps-mobile`; tabs and detail pages cover bookings, request submission, notifications, profile, auth. | Implemented for current employee baseline. |
| Notification | Exchange support. | Notification consumes Booking events and provides in-app, email, API, stream, unread count, mark-read, preferences. | `FPS.Notification` service exists; Dapr `booking-events` consumer, APIs, broadcaster, email boundary, preferences. | Implemented. |
| Event Bus | Exchange support. | Dapr pub/sub is the event bus boundary for Booking events. | Dapr `fps-pubsub` components exist for local/demo/client/smoke; Booking publishes and Notification/Audit/Reporting subscribe. | Implemented for current event flow. |
| Communication | Exchange support. | Current practical communication is Notification in-app/email plus gateway/client traffic. SMS/push are not current baseline. | Email sender boundary and SSE exist; no SMS or push service found. | Partial. Keep as target support capability, not current feature. |
| Booking | Bookings Management. | Booking owns request lifecycle, allocation, cancellation, usage, draw, HR operations support, event publication. | `FPS.Booking` API/application/domain/infrastructure exist with tests. | Implemented for current parking baseline. |
| Allocation | Reference under Bookings Management. | Allocation rules and Draw are core to Booking, not a separate server. | Allocation lives inside Booking domain/application services. | Implemented inside Booking. Diagram may imply a separate function, not a separate service. |
| User | Reference under Bookings Management. | User identity/profile facts are owned by Identity/Profile/Customer, not Booking. | Current user is shared kernel/auth context; Profile owns user/profile snapshot. | Implemented across Identity/Profile. Rename/reference carefully to avoid implying Booking owns users. |
| Customer | Customer Services. | Customer manages tenant lifecycle, identity setup, parking bootstrap, first admins, readiness, and future support/customer service. Tenant storage contract requires Customer tenant registry, onboarding state, and identity configuration to be physically stored. | `FPS.Customer` service exists with tenant, identity, parking bootstrap, readiness APIs, but registers `InMemoryTenantRepository`, `InMemoryTenantIdentityRepository`, and `InMemoryTenantParkingBootstrapRepository`. No `customerstore` Dapr state component exists. | Main gap. Customer foundation exists, but tenant/customer state is not durable. |
| Feedback | Customer Services. | Feedback docs currently mark it deferred; security docs define sensible controls for feedback data. | No `code/server/Feedback` service and no web/mobile feedback page found. | Target capability not implemented. Reasonable candidate for a small testing/evaluator feedback slice. |
| Billing | Finance. | Billing is deferred until commercial offer is approved. | No `code/server/Billing` service. | Target capability only. Not a make-the-app-work priority. |
| Payment Gateway | Finance. | Payment/financial collection is outside current scope. | No payment provider/gateway code found. | Target/reference only. Defer with Billing. |
| Profile | IS Resources Management. | Profile owns employee/profile facts, vehicle facts, eligibility, HR import/bootstrap. | `FPS.Profile` service exists with snapshot, bootstrap/import, admin seed, erasure placeholder. | Implemented for current baseline, with durable erasure/storage gaps still tracked elsewhere. |
| Configuration | IS Resources Management. | Configuration owns policy, locations/slots, history, publication and audit. | `FPS.Configuration` service exists with policy and slot APIs/history. Booking still uses local policy/capacity stubs for runtime decisions. | Configuration implemented; Booking integration partial. |
| Support, HR | Business area. | HR Operations workspace and HR import exist; support workflow is not a broad service desk. | Web has `HrOperationsPage` and `HrImportPage`; Booking has operations and HR cancel APIs. | HR operations implemented for current baseline. General support desk is not implemented. |
| Reporting | Reporting. | Reporting consumes Booking outcomes and exposes dashboard, fairness, utilization, reason-code, and exports. | `FPS.Reporting` service and web Reporting page exist, but Reporting currently registers `InMemoryReportingRepository`. | Partial. Report catalog/endpoints exist; durable relational read model is needed for customer readiness. |
| Identity | Security/Auth. | Identity/auth context, OIDC, tenant/user/role claims, role mapping, local fallback. | `FPS.Identity`, shared auth kernel, Keycloak local realm, web/mobile OIDC config exist. | Implemented for current baseline. |
| Identity & Access Management | Security/Auth. | Tenant identity setup and role mapping are Customer/Identity responsibilities. | Customer identity config and shared tenant role mapper exist. | Implemented, split across Customer and Shared/Identity. |
| API Gateway | Security/Auth. | Gateway is deployment-profile specific; public/local Envoy configs route clients to services. | `code/infrastructure/envoy/envoy.yaml` and `envoy-public.yaml` exist. | Implemented as infrastructure profile, not app code. |
| Secure Storage | Security/Auth. | Secrets use Dapr secret store; production store selected by deployment. | Dapr `vault.yaml`, `vault-demo.yaml`, Vault config and docs exist. | Implemented as local/demo/client boundary; production operation remains deployment-specific. |
| Dapr Workflows | Workflow Management. | Used where needed, especially privacy erasure. | Audit has Dapr Workflow privacy/erasure orchestration code and models. | Implemented for privacy erasure path, not a general workflow engine for all functions. |
| Dapr | Frameworks. | Dapr is the provider-neutral boundary for pub/sub, state, bindings, service invocation, secrets, and future workflows. | Dapr components and health checks exist; services use Dapr client for state/pubsub. | Implemented as core runtime boundary. |
| File Storage | Document Management. | Object storage is deployment-selected and used where required. | Local Dapr `s3store.yaml` exists; no broad document-management feature found. | Infrastructure boundary exists; product document management is not implemented. |
| Cache | Data Management. | Cache/session store is a deployable boundary where needed. | Docs describe selected cache/session store; no strong application cache feature found. | Target infrastructure capability, not central current app behavior. |
| Relational Data Store | Data Management. | Deployment can choose operational/document stores behind Dapr boundaries. | Current local profile uses MongoDB through Dapr state stores; no active PostgreSQL app persistence path found. | Good fit for Reporting read models. Not needed for all services, but should become the Reporting store. |
| Document Data Store | Data Management. | Dapr state/document store is the current persistence boundary. | Local Dapr state stores use `state.mongodb` for booking, audit, notification, profile, configuration, and reporting templates. | Implemented for service state generally. Reporting should move from generic document/state storage direction to PostgreSQL read models. |
| Logging & monitoring | Audit Trails / transversal. | Technical logs/metrics/traces are separate from Audit business evidence; OpenTelemetry/Grafana/Loki/Prometheus local evidence exists. | Shared OpenTelemetry extensions, request trace logging, Grafana/Prometheus/Loki/Promtail configs and alert rules exist. | Implemented for local/evaluation baseline. |
| Audit | Audit Trails. | Audit is business evidence, not raw technical logs. | `FPS.Audit` service exists with Booking event consumer, query, erasure, retention, integrity, export, privacy workflow. | Implemented. |
| Reference engines | Customer Engine, Booking Engine, Feedback Engine, Billing Engine, Audit Engine, Reporting Builder. | Docs have service-specific pages for implemented domains; Billing and Feedback are deferred/target. | Implemented engines map to actual services for Booking/Audit/Reporting/Customer. Feedback/Billing engines do not exist; Customer Engine is only partial. | Mixed. Treat as target reference taxonomy, not deployed component list. |

## Documentation Gaps

- [docs/business-layer/functional-architecture.md](./business-layer/functional-architecture.md) is too broad in places for the current product baseline. It includes social login, biometric auth, SMS, revenue reports, violations, payment receipts, broad feedback management, and support-desk-like workflows that are not current app priorities.
- [docs/application-layer/function-map.md](./application-layer/function-map.md) only embeds the image and does not explain current versus target status.
- Customer Service needs durable persistence: current tenant readiness/onboarding foundation versus missing physical storage for tenant registry, identity configuration/admins, and parking bootstrap state.
- [docs/application-layer/feedback.md](./application-layer/feedback.md) and [docs/business-layer/feedback.md](./business-layer/feedback.md) say Feedback is deferred. That is mostly true, but a narrow authenticated testing/evaluator feedback slice now looks reasonable.
- Billing docs are correctly guarded as deferred and should stay that way.

## Recommended Function Map Updates

When the function map is updated, keep it as a target map but visually distinguish:

1. Current baseline: Web App, Mobile App, Booking, Profile, Configuration, Identity, Notification, Audit, Reporting endpoints, Event Bus, API Gateway, Dapr, MongoDB-backed Dapr state stores, observability.
2. Partial/current-boundary items: Customer Service durable storage, Reporting PostgreSQL read model, Configuration-to-Booking runtime exchange, Dapr Workflows, File/Object Storage, Secure Storage, Communication, Support/HR.
3. Future/deprioritized: Billing, Payment Gateway, relational store as a generic reference, broad support desk.
4. Candidate near-term testing support: Feedback.

## Implementation Follow-Ups

- Define and implement the Customer durable storage slice needed for app/customer-readiness: Dapr-backed tenant registry, identity configuration/admins, parking bootstrap state, and local/demo/client `customerstore` components.
- Define and implement the Reporting durable relational store slice: PostgreSQL-backed projections, event idempotency table, tenant-scoped report queries, and restart-persistence evidence.
- Prioritize making the app work end to end before Billing or payment work.
- Prepare a small Feedback slice for customer testing if Robert approves:
  - authenticated web/mobile feedback submission;
  - tenant/user context from auth only;
  - category, message, optional page/context, status;
  - admin/support view;
  - notification optional, not required in the first slice;
  - server-side validation and warning not to submit secrets or unrelated personal data.
- Keep broad feedback dashboard, attachments, email responses, analytics, and support-desk workflow out of the first slice.
- Keep Billing and Payment Gateway out of current customer-first implementation unless the commercial model changes.
