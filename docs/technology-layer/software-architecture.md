---
title: Software Architecture
---

![Logical Architecture](../images/fps-logical-architecture.png)

## Architecture Overview

FPS is a multi-tenant, event-driven SaaS platform. The system is organised as independently deployable services around bounded business contexts, with Dapr providing sidecar-based service integration, pub/sub, health checks, and future workflow orchestration.

Each backend service follows the same high-level shape:

- API layer for HTTP endpoints and Dapr subscriptions;
- Application layer for commands, queries, handlers, and cross-service ports;
- Domain layer for aggregates, value objects, and business rules;
- Infrastructure layer for persistence, Dapr clients, HTTP clients, and adapters.

Current implementation work is documentation-led and proceeds through vertical slices in [Development Plan](../development-plan). Booking Phase 1 is complete, and the active integration sequence has added authenticated context, Profile snapshots, Notification event consumption, and Audit event consumption.

## Context Boundaries

| Context | Responsibility | Current integration role |
| --- | --- | --- |
| Identity | Authenticated user context, roles, and `GET /me`. | JWT claim mapping for tenant, user, and roles. |
| Booking | Parking request lifecycle, draw/allocation decisions, cancellation, usage, and booking history. | Core source of Booking events. |
| Profile | Employee parking eligibility, vehicle facts, company-car and accessibility entitlements. | Booking consumes immutable Profile snapshots. |
| Notification | User-visible notification records and later channels. | Consumes Booking events and stores idempotent in-app records. |
| Audit | Append-only, pseudonymised trace of Booking events. | Consumes Booking events and stores audit records. |
| Configuration | Tenant/location parking policy and slot/capacity inputs. | Booking consumes policy and capacity contracts. |
| Customer | Tenant ownership and onboarding. | Defines tenant boundaries and future provisioning. |
| Reporting | Materialised read models and analytics. | Consumes Booking events/read models without driving Booking state. |
| Billing | Subscription and invoice workflows. | Future metering and invoicing. |

Boundary rules are defined in [Booking Context Contract](../business-layer/booking-context-contract), [Booking Event Contracts](../business-layer/booking-event-contracts), and [Booking Authorization](../business-layer/booking-authorization).

## Application Components

| App. Component | Name | Technology |
|------------------- | ---- | ------- |
| [Web App](./web-app) | Web App | React |
| [Mobile App](./mobile-app) | Mobile App | React Native + Expo |
| [Identity](./identity) | Authentication & Authorization | .NET 10 Web API |
| [Audit](./audit) | Audit Service | .NET 10 Web API |
| [Billing](./billing) | Billing Service | .NET 10 Web API |
| [Booking](./booking) | Booking Service | .NET 10 Web API |
| [Configuration](./configuration) | Configuration Service | .NET 10 Web API |
| [Customer](./customer) | Customer Service | .NET 10 Web API |
| [Notification](./notification) | Notification Service | .NET 10 Web API |
| [Profile](./profile) | Profile Service | .NET 10 Web API |
| [Reporting](./reporting) | Reporting Service | .NET 10 Web API |
| [Feedback](./feedback) | Feedback Service | .NET 10 Web API |

## Integration Model

| Integration | Pattern | Notes |
| --- | --- | --- |
| API access | HTTP through Traefik/API gateway | JWT-based authentication, service endpoints stay tenant-aware. |
| Service-to-service command data | Synchronous HTTP/Dapr where required for command decisions | Booking may synchronously query Configuration and Profile when required to accept/reject a command. |
| Domain outcomes | RabbitMQ via Dapr pub/sub | Booking events feed Notification, Audit, and future Reporting read models. |
| Workflow/orchestration | Dapr Workflows where durability is needed | Draw workflow can be introduced when operational replay/long-running orchestration is required. |
| Write persistence | Dapr state store backed by MongoDB | Aggregate persistence; tenant isolation is database-per-tenant. |
| Read persistence | MongoDB driver/read models | Query/projection stores per tenant. |

Cross-domain failures follow [Booking Context Contract](../business-layer/booking-context-contract): required command inputs fail safely, while observer services such as Notification and Audit must not roll back persisted Booking state.

## Other Components

| App. Component | Software Component | Name | Technology |
|------------------- | ------------------- | ---- | ------- |
| Authentication | Keycloak | Identity and Access Management | Java |
| Traces and metrics | Prometheus | Monitoring and Alerting | Go |
| Monitoring | Grafana | Analytics and Monitoring | Various |
| Logging | Loki | Log Aggregation | Go |
| Tracing | Jaeger | Distributed Tracing | Go |
| Write store (CQRS) | Dapr State Store → MongoDB | Aggregate persistence per tenant database | Various |
| Read store (CQRS) | MongoDB driver | Query/projection read models per tenant database | Various |
| Event Bus | RabbitMQ via Dapr pub/sub | Message Broker | Various |
| Cache | Redis | In-Memory Data Store | Various |
| API Gateway | Traefik | Edge Router | Go |
| File Storage | MinIO | Object Storage | Go |
| Secret Management | Vault | Secret Management | N/A |

> **Multi-tenancy**: each tenant is isolated in its own MongoDB database (`fps_{tenant_id}`). Services resolve the tenant database from the request context before any read or write operation.

## Security

FPS security is centred on authenticated context, tenant isolation, least privilege, and traceability.

| Concern | Architecture decision |
| --- | --- |
| Identity provider | Keycloak/OIDC provides JWTs. Stable claim mapping is documented in [Versions and Decisions](../versions-and-decisions). |
| Current user context | Services resolve `tenantId`, `userId`, and roles from authenticated claims through `ICurrentUser`; request bodies, query strings, or caller-supplied identity headers must not override identity. |
| Tenant isolation | MongoDB database-per-tenant (`fps_{tenant_id}`), resolved from authenticated/service context before reads or writes. |
| Service-to-service security | Dapr mTLS/Sentry is the platform baseline; user-context forwarding is used only where the downstream service must make a user-scoped decision. |
| Authorization | Booking authorization rules are documented in [Booking Authorization](../business-layer/booking-authorization); services must fail closed on missing required identity claims. |
| Privacy | Audit stores pseudonymised user references (`actorHash`, requestor/affected-user hashes) and must not store raw names, emails, profile private data, or raw user IDs in audit records. |
| Event safety | Booking events must not include secrets, stack traces, lottery seeds, internal weights, or private details about unrelated employees. |
| Secrets | Vault is the target secret store for credentials and API keys. |
| Observability | Prometheus, Grafana, Loki, and Jaeger provide metrics, dashboards, logs, and distributed tracing. |

Detailed security documentation is maintained under [Security](./security), especially [Authentication](./security/authentication), [Authorization](./security/authorization), [Data Privacy](./security/data-privacy), [Traceability](./security/traceability), and [Microservice Security Patterns](./security/microservice-security-patterns).

### Dapr

| Software Component | Name | Type | Purpose | Packaging Type |
|------------------- | ---- | ---- | ------- | -------------- |
| Dapr Operator | dapr-operator | Service | Manages component updates and Kubernetes services endpoints for Dapr (state stores, pub/subs, etc.) | Docker container |
| Dapr Sidecar Injector | dapr-sidecar-injector | Service | Injects Dapr sidecar into application pods | Docker container |
| Dapr Sentry | dapr-sentry | Service | Manages mTLS certificates for Dapr services | Docker container |
| Dapr Placement | dapr-placement | Service | Manages actor placement for Dapr actors | Docker container |
| Dapr Dashboard | dapr-dashboard | UI | Web UI for managing and monitoring Dapr applications | Docker container |

## Licensing

FPS is licensed under AGPL-3.0-or-later. The repository license decision is recorded in [Versions and Decisions](../versions-and-decisions), and the full license text is available in [LICENSE](../../LICENSE).

## Tool/Framework Versions

This section provides a list of tools and frameworks used in the project, along with their versions, preferred editors, programming languages, distribution formats, and licenses.

| Tool/Framework | Version | Editor | Language | Distribution Format | License | Purpose |
| ---------------| ------- | ------ | -------- | ------------------- | ------- | ------- |
| React          | Current project version | VSCode | TypeScript/JavaScript | npm package | MIT | Frontend library for building user interfaces |
| React Native  | 0.74+   | VSCode | TypeScript | npm package         | MIT     | Cross-platform mobile app framework |
| Expo          | 51+     | VSCode | TypeScript | npm package         | MIT     | Managed React Native workflow — no native build tooling required |
| .NET 10        | 10.0    | VSCode | C#        | NuGet package       | MIT     | Framework for building various types of applications |
| Java           | 11      | IntelliJ| Java     | JAR file            | GPL     | General-purpose programming language |
| Docker         | 20.10.7 | VSCode | N/A      | Docker image        | Apache 2.0 | Platform for developing, shipping, and running applications in containers |
| Helm           | 3.5.4   | VSCode | N/A      | Helm chart          | Apache 2.0 | Package manager for Kubernetes applications |
| Dapr           | 1.14+   | VSCode | Various  | Docker image        | Apache 2.0  | Runtime for building distributed applications (Workflows require 1.10+) |
| Kubernetes     | 1.21.0  | VSCode | YAML     | Helm chart          | Apache 2.0 | Container orchestration platform |
| Terraform      | 1.0.0   | VSCode | HCL      | Binary              | MPL 2.0 | Infrastructure as code tool |
| Ansible        | 2.9.0   | VSCode | YAML     | Package             | GPL 3.0 | Automation tool for IT tasks |
| Git            | 2.31.1  | VSCode | N/A      | Binary              | GPL 2.0 | Version control system |
