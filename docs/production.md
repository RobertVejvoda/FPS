# Production

Production describes how FPS is hosted, operated, recovered, and validated once it runs outside local development. It is a top-level architecture section because production concerns cut across the technology stack, security model, implementation slices, and business continuity expectations.

The goal for v1 is not to operate production for clients directly. FPS must prove that the platform can be run locally, demonstrated in a realistic hosted environment, and deployed into a client-owned production environment with clear operational evidence. Dapr is the component portability boundary; OpenTelemetry is the telemetry portability boundary.

## Production Story

Read this section from high level to detail:

1. **Environment profiles**: separate local development, demo, and client-owned production responsibilities.
2. **Target runtime**: understand what must run and which cloud services are replaceable behind Dapr.
2. **Availability and recovery**: define what can fail, how FPS keeps operating, and how much data loss/downtime is acceptable.
3. **Data protection**: define backups, restore drills, tenant-scoped recovery, and secret recovery.
4. **Operations**: define monitoring, alerts, incidents, maintenance, and runbooks.
5. **Cloud setup**: compare candidate deployment profiles and keep cost visible.
6. **Testing and readiness**: prove the environment before calling it production.

## Environment Profiles

| Profile | Owner | Purpose | Expected shape |
| --- | --- | --- | --- |
| Local | FPS delivery team | Develop and validate behavior cheaply. | Docker Compose or local containers, local Dapr sidecars/components, MongoDB, RabbitMQ, Redis, Prometheus/Grafana, and local tracing. |
| Demo | FPS delivery team | Show a working system to evaluators and collect performance/usage evidence. | Low-cost hosted deployment using replaceable Dapr components; exact provider remains a planning decision. |
| Client production | Client IT / operations | Run FPS with the client's identity, hosting, monitoring, backup, and security controls. | Client-owned cloud or on-premise environment, Dapr-compatible components, OpenTelemetry export to the client's observability platform, documented backup/restore and support boundaries. |

## Target Runtime

FPS production runtime is expected to contain:

| Capability | Production role | Current direction |
| --- | --- | --- |
| Container hosting | Runs the .NET services, web app, and supporting workers. | Replaceable by profile: local containers, low-cost demo hosting, or client-owned platform. |
| Dapr sidecars | Service invocation, pub/sub, state-store integration, secret access, and future workflows. | Dapr remains the portability boundary. |
| API ingress | Public HTTPS entry point and routing to services. | Traefik or cloud-native ingress, with TLS and rate limiting. |
| Identity provider | OIDC/OAuth login, JWT claims, roles, and tenant/user context. | Keycloak or cloud-managed equivalent. |
| Write/read persistence | Service-owned MongoDB databases with tenant-specific collections. | MongoDB-compatible managed or self-hosted option. |
| Message broker | Booking events to Notification, Audit, Reporting, and future consumers. | RabbitMQ via Dapr pub/sub. |
| Cache/session support | Cache, rate limiting, and short-lived operational state. | Redis-compatible service. |
| Secret management | Credentials, certificates, API keys, and deployment secrets. | Vault or cloud-native secret store. |
| Object storage | Reports, exports, backup artifacts, and future attachments. | S3-compatible storage such as MinIO or cloud object storage. |
| Observability | Metrics, logs, traces, dashboards, alerting, and usage evidence. | Local Prometheus/Grafana; demo stack to prove behavior; client production exports through OpenTelemetry to platforms such as Dynatrace, Azure Monitor, Grafana, Splunk, or equivalent. |

## Operational Pages

- [Availability Model](./production/availability-model): service, data, broker, identity, and deployment failure assumptions.
- [RTO/RPO Requirements](./production/rto-rpo-requirements): recovery time and recovery point targets by capability.
- [Backup And Restore](./production/backup-restore): backup scope, restore order, tenant-scoped restore, and restore evidence.
- [Monitoring](./production/monitoring): metrics, logs, traces, dashboards, alerts, and cloud-provider monitoring options.
- [Incident Handling](./production/incident-handling): incident lifecycle, severity, communication, evidence, and follow-up.
- [Maintenance](./production/maintenance): patching, upgrades, rollbacks, secret rotation, and operational upkeep.

## Cloud And Environment Pages

- [Development Setup](./production/dev-setup): local/open-source stack used to approximate production dependencies.
- [AWS Setup](./production/aws-setup): AWS-oriented cost and service mapping.
- [Azure Setup](./production/azure-setup): Azure-oriented cost and service mapping.
- [Hosting and Deployment Strategy](./production/hosting-deployment-strategy): deployment profile strategy covering local, demo, and client-owned production with Dapr component portability and cost planning.

The cloud setup pages are reference inputs only. `OPS000` selected the need for a pluggable Dapr-first strategy, not a final production provider owned by FPS.

## Testing And Readiness

- [Testing](./production/testing): test types used to build production confidence.
- [Testing Scenarios](./production/testing-scenarios): concrete production-readiness scenarios to validate before hosted pilot.

Minimum readiness before a hosted pilot:

- deployment can be repeated without manual server edits;
- ingress uses HTTPS and documented hostnames;
- tenant and user context come from the identity provider;
- MongoDB tenant collections and indexes are provisioned repeatably;
- RabbitMQ/Dapr pub-sub is configured and validated;
- secrets are injected from a secret-management system, not committed files;
- backup and restore have been tested at least once;
- metrics, logs, traces, usage counters, and alert routing exist;
- telemetry can be exported through OpenTelemetry to a client monitoring platform;
- incident and rollback runbooks are documented;
- `./tools/validate.sh` and relevant frontend/mobile checks pass before deployment.

## Slice Mapping

Production work is tracked through these slices:

| Slice | Purpose |
| --- | --- |
| `OPS000` | Hosting and deployment strategy options; merged as the baseline for pluggable environments. |
| `OPS001` | Pluggable Dapr component baseline, tenant collection/index provisioning, secrets, and runbooks. |
| `OPS002` | Demo environment baseline and cost model for evaluation. |
| `OPS003` | Client-owned production integration guide. |
| `OPS004` | Observability, performance evidence, backup/restore verification, and production runbooks. |

Production pages should be updated whenever these slices change the target hosting model, deployment path, backup strategy, monitoring stack, or operational responsibilities.
