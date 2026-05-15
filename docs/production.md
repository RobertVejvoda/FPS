# Production

Production describes how FPS is hosted, operated, recovered, and validated once it runs outside local development. It is a top-level architecture section because production concerns cut across the technology stack, security model, implementation slices, and business continuity expectations.

The goal for v1 is a low-cost hosted pilot that keeps Dapr as the portability boundary. The first cloud environment does not need to be multi-region or enterprise-scale, but it must prove that FPS can be deployed, monitored, backed up, restored, and operated with clear evidence.

## Production Story

Read this section from high level to detail:

1. **Target runtime**: understand what must run in production and which cloud services are replaceable behind Dapr.
2. **Availability and recovery**: define what can fail, how FPS keeps operating, and how much data loss/downtime is acceptable.
3. **Data protection**: define backups, restore drills, tenant-scoped recovery, and secret recovery.
4. **Operations**: define monitoring, alerts, incidents, maintenance, and runbooks.
5. **Cloud setup**: compare candidate cloud layouts and keep cost visible.
6. **Testing and readiness**: prove the environment before calling it production.

## Target Runtime

FPS production runtime is expected to contain:

| Capability | Production role | Current direction |
| --- | --- | --- |
| Container hosting | Runs the .NET services, web app, and supporting workers. | Kubernetes or a lower-cost container service chosen by `OPS000`. |
| Dapr sidecars | Service invocation, pub/sub, state-store integration, secret access, and future workflows. | Dapr remains the portability boundary. |
| API ingress | Public HTTPS entry point and routing to services. | Traefik or cloud-native ingress, with TLS and rate limiting. |
| Identity provider | OIDC/OAuth login, JWT claims, roles, and tenant/user context. | Keycloak or cloud-managed equivalent. |
| Write/read persistence | Service-owned MongoDB databases with tenant-specific collections. | MongoDB-compatible managed or self-hosted option. |
| Message broker | Booking events to Notification, Audit, Reporting, and future consumers. | RabbitMQ via Dapr pub/sub. |
| Cache/session support | Cache, rate limiting, and short-lived operational state. | Redis-compatible service. |
| Secret management | Credentials, certificates, API keys, and deployment secrets. | Vault or cloud-native secret store. |
| Object storage | Reports, exports, backup artifacts, and future attachments. | S3-compatible storage such as MinIO or cloud object storage. |
| Observability | Metrics, logs, traces, dashboards, and alerting. | Prometheus, Grafana, Loki, Jaeger, or managed equivalents. |

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

These cloud pages are candidate inputs for `OPS000`, not final decisions. `OPS000` should select the first hosted path with Dapr portability and cost as explicit criteria.

## Testing And Readiness

- [Testing](./production/testing): test types used to build production confidence.
- [Testing Scenarios](./production/testing-scenarios): concrete production-readiness scenarios to validate before hosted pilot.

Minimum readiness before a hosted pilot:

- deployment can be repeated from CI/CD without manual server edits;
- ingress uses HTTPS and documented hostnames;
- tenant and user context come from the identity provider;
- MongoDB tenant collections and indexes are provisioned repeatably;
- RabbitMQ/Dapr pub-sub is configured and validated;
- secrets are injected from a secret-management system, not committed files;
- backup and restore have been tested at least once;
- monitoring dashboards and alert routing exist;
- incident and rollback runbooks are documented;
- `./tools/validate.sh` and relevant frontend/mobile checks pass before deployment.

## Slice Mapping

Production work is tracked through these slices:

| Slice | Purpose |
| --- | --- |
| `OPS000` | Hosting and deployment strategy options. |
| `OPS001` | Local/production Dapr hardening, tenant collection/index provisioning, secrets, and runbooks. |
| `OPS002` | Cloud environment baseline for the first hosted pilot. |
| `OPS003` | Observability, backup/restore verification, and production runbooks. |

Production pages should be updated whenever these slices change the target hosting model, deployment path, backup strategy, monitoring stack, or operational responsibilities.
