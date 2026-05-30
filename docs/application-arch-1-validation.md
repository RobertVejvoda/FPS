# Application Architecture 1 Validation

This note validates `docs/images/fps-application-arch-1.png` against the current documentation and implementation. The diagram is treated as a target application view, not a claim that every component is production-complete today.

## Alignment Summary

Application Architecture 1 is broadly aligned with the intended FPS application shape:

- Browser and mobile clients call the backend through an API gateway.
- Authentication is handled by the Identity boundary and external IAM/OIDC provider.
- Booking, Profile, Configuration, Customer, Reporting, Audit, Notification, Billing, and Feedback are represented as application services.
- Booking-domain events flow through the event bus to Audit, Notification, and Reporting.
- Dapr-backed infrastructure is reflected through document storage, pub/sub, secret storage, observability, and local dashboards.

The main correction is status, not structure: several boxes are target or partial capabilities and should not be read as customer-ready implementation.

## Implemented Or Aligned

- API Gateway is represented correctly as the entry point for backend APIs. The current implementation uses Envoy gateway configuration.
- Identity and IAM are aligned with the SSO/OIDC direction. Clients authenticate with the IAM provider and then call APIs with tenant-scoped identity through the gateway and services.
- Booking is the main implemented business service and publishes events consumed by other services.
- Profile and Configuration exist as service boundaries and support the current application flow.
- Audit is aligned as a business audit service and must remain distinct from technical logs.
- Notification is aligned as a service boundary for user-facing notifications. Current implementation focuses on in-app/server-sent notification behavior rather than every future communication channel.
- Event Bus is aligned with the Dapr pub/sub direction.
- Document DB / NoSQL storage is aligned for the existing Dapr state-store pattern used by most service state.
- Secret Storage is aligned with Dapr secret-store and Vault direction.
- Logging, Monitoring, and Local Dashboard are aligned with local observability using OpenTelemetry, Prometheus, Grafana, Loki, Promtail, and related tooling.

## Known Gaps

### Billing And Payment Gateway

Billing and Payment Gateway are valid target architecture elements, but they are not a current customer-first priority. Keep them visible as future commercial capability, but do not route near-term implementation work there unless the product priority changes.

### Customer Durable Storage

Customer is represented correctly as an application service, but durable persistence is still a readiness gap. The current code uses in-memory tenant repositories for tenant registry, tenant identity configuration, first administrators, and parking bootstrap data.

This is tracked as `DATA011: Customer durable tenant storage`.

### Reporting Storage

Reporting is represented correctly as an application service, but it should not be backed by the same document-store assumption as operational service state. For customer-ready reporting, FPS needs durable relational read models, preferably PostgreSQL-backed projections fed by Booking events.

This is tracked as `REPORT004: Durable relational reporting store`.

### Feedback

Feedback is a valid target boundary and is useful for testing/customer-evaluator feedback, but it remains mostly deferred. A small authenticated feedback slice is reasonable before billing work because it helps collect customer pilot feedback without making Billing a dependency.

### Communication Channels

The Communication dependency is directionally correct, but current notification behavior is narrower than the future architecture. Email, push, SMS, provider retries, and channel preference handling should be treated as staged capabilities.

### File Storage

File/Object Storage exists as an infrastructure direction through MinIO/S3-style binding, but broad product document-management behavior is not a current customer-first feature.

### Cache

Cache is reasonable as a technical architecture dependency, but it is not central to the current functional path. It should stay optional until a specific performance or coordination requirement makes it necessary.

## Diagram Interpretation

Use the diagram as a target application architecture with the following status split:

1. Customer-first baseline: Web App, Mobile App, API Gateway, Identity/IAM, Booking, Profile, Configuration, Notification, Audit, Reporting endpoints, Dapr pub/sub, Dapr state stores, secrets, and observability.
2. Customer-readiness gaps: Customer durable storage and Reporting relational persistence.
3. Partial or staged capabilities: Feedback, Communication channels, File/Object Storage, Cache.
4. Future/deprioritized capabilities: Billing and Payment Gateway.

## Recommended Next Actions

- Keep Application Architecture 1 as the target view, but annotate status in docs rather than redrawing it as a delivery board.
- Complete the Customer durable storage slice before relying on tenant onboarding or tenant identity setup across restarts.
- Complete the Reporting PostgreSQL read-model slice before promising durable customer reporting.
- Keep Billing and Payment Gateway out of the customer-first deployable scope for now.
- Consider a small authenticated Feedback slice for pilots and demos after the P0 persistence gaps are moving.
