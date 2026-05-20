# OPS003 Client-Owned Production Integration

This page defines the responsibilities, boundaries, and checklist for handing FPS to a client-owned production environment. It is the companion to the deployment profile strategy in [Hosting And Deployment Strategy](./hosting-deployment-strategy).

## Operational Responsibility Split

| Area | FPS Delivery Team | Client IT / Operations |
| --- | --- | --- |
| Container images | Builds, tests, and publishes per release. | Pulls images from the agreed registry or builds from source. |
| Application configuration | Supplies required env var names, Dapr component contracts, and appsettings schema. | Provides env var values, secrets, and component-specific configuration for the client environment. |
| Identity provider | Documents required OIDC claims and tenant/role mapping requirements. | Operates the IdP (Keycloak, Azure Entra ID, Okta, or equivalent), maintains realm/client configuration, and manages user lifecycle. |
| Secret management | Documents which secrets are required and how they are referenced through Dapr secretstore. | Provisions and rotates secrets in the client-approved secret store (Vault, Key Vault, AWS Secrets Manager, or equivalent). |
| Infrastructure | Provides Docker Compose, Dapr component YAML templates, and sizing assumptions. | Provisions container hosting, databases, broker, cache, secret store, object storage, and networking. |
| Database provisioning | Documents collection-per-tenant naming convention, required indexes, and backup/restore procedures. | Creates MongoDB databases, provisions collections, applies indexes, and manages backup schedules and retention. |
| Observability | Instruments services with OpenTelemetry; documents required signals. | Configures OTLP collector/exporter for the client monitoring platform, defines alert thresholds, and owns operational dashboards. |
| Backup and restore | Provides restore runbooks and test procedures. | Schedules backups, tests restores, and owns the recovery time and recovery point targets for production. |
| Release | Publishes release notes and migration guidance per release. | Decides when to apply releases, runs pre-release smoke checks in the client environment, and maintains rollback capability. |
| Incident response | Available for escalation on application defects. | Owns first-line incident response, platform incidents, and client-specific data or access issues. |
| GDPR / data protection | Provides pseudonymisation design, erasure procedure, and PII mapping documentation. | Signs Data Processing Agreement, implements retention schedules, handles erasure requests, and owns data residency. |

## Dapr Component Replacement Boundaries

FPS uses Dapr building blocks as the portability boundary. No application service code changes when a component is swapped; only the component YAML changes.

| Building block | Required contract | Local baseline | Client production examples |
| --- | --- | --- | --- |
| Pub/sub | Topic names match `code/infrastructure/dapr/README.md`. Component name must be `fps-pubsub`. | RabbitMQ | Azure Service Bus, AWS SNS/SQS, GCP Pub/Sub, Apache Kafka |
| State store | Collection-per-tenant naming. No cross-tenant state keys. | MongoDB (in-memory for smoke) | MongoDB Atlas, Azure Cosmos DB (MongoDB API), AWS DocumentDB |
| Secret store | Secret names match service configuration. Dapr secretstore reference pattern only; no inline secrets. | Vault (local) | HashiCorp Vault, Azure Key Vault, AWS Secrets Manager, GCP Secret Manager |
| Bindings (cron) | Schedule expression in Dapr cron binding format. | Dapr local cron | Dapr cron binding, platform scheduler |
| Service invocation | App ID naming matches `dapr.yaml` app IDs. | Dapr self-hosted | Dapr in managed runtime or Kubernetes |
| mTLS | Enabled in production Dapr configuration (`fps-config.yaml`). | Disabled for local smoke | Platform-managed Dapr mTLS or Sentry |

Component YAML templates are in `code/infrastructure/dapr/components/`. Local files use in-memory components for smoke testing. Production files must:
- scope components to the required app IDs;
- use `secretstoreref` pattern instead of inline credentials;
- enable mTLS and match the client's network security policy.

## Identity Integration Requirements

FPS validates JWT tokens using the OIDC standard. The client IdP must satisfy:

| Requirement | Detail |
| --- | --- |
| OIDC/OAuth 2.0 | Standard Authorization Code + PKCE flow for mobile/web; client credentials or ROPC only for internal tooling. |
| Issuer | Token `iss` must match the configured `Auth:Authority` per service. FPS trusts one issuer per service instance. |
| Audience | Token `aud` must match the configured `Auth:Audience`. |
| Subject | Token must contain a stable immutable subject (`sub` or equivalent). Used as `userId` in all FPS contexts. |
| Tenant ID | Token must contain a `tenant_id` claim matching the client's configured FPS tenant. Never accept tenant from request bodies. |
| Roles | IdP groups are mapped to FPS roles via the `TenantRoleMapping` configuration section in each service (`ConfiguredTenantRoleMapper`). Supported FPS roles: `admin`, `hr_manager`, `auditor`, `report_viewer`, `employee`. |
| Token lifetime | Short-lived access tokens (15–60 minutes). Refresh tokens where the flow requires them. |
| User deactivation | Deactivating a user in the IdP prevents new token issuance. FPS services also support a fast-path in-memory deactivation store (`IDeactivatedUserStore`) for same-session denial. |

See `docs/security/security-model.md` for the data classification and SSO-first integration contract.

## Network And Security Assumptions

| Boundary | Requirement |
| --- | --- |
| External ingress | HTTPS with TLS 1.2+. Client-managed certificate. |
| Internal service traffic | Dapr service invocation with mTLS enabled. Services are not directly reachable from outside the container network. |
| Database | Private network access only. Connection string provided via Dapr secretstore, not environment variable. |
| Message broker | Private network access. Credentials provided via Dapr secretstore. |
| Secret store | Reachable from Dapr sidecars at runtime. Vault token or cloud credential via workload identity where supported. |
| Observability | OTLP endpoint reachable from services. No sensitive data (tokens, passwords, PII) in telemetry labels. See `integration-secrets.md`. |
| Admin access | Time-bound, audited, and restricted to named operators. No standing admin access to production data. |

## Backup And Restore Handoff

FPS provides procedures; the client owns execution and scheduling.

| Scope | Procedure | Client responsibility |
| --- | --- | --- |
| MongoDB service databases | Export per-service collection sets using `mongodump`. See `docs/production/backup-restore`. | Schedule automated backups. Define retention period per GDPR requirements. |
| Secret store | Vault snapshot or cloud key vault export. | Own rotation schedule and backup of secret material. |
| Keycloak realm | Realm export (`/auth/admin/realms/fps-local/export`). | Periodic realm backup and version-tagged export before upgrades. |
| Restore drill | Follow `docs/production/backup-restore` restore procedures and record evidence. | Run restore drill at least quarterly. Record time-to-restore and data loss. |
| Tenant-scoped restore | Restore from a per-service `mongodump` scoped to the affected tenant collection. | Test per-tenant restore path before going live with first tenant. |

## Release Process

| Step | Who | Notes |
| --- | --- | --- |
| Release published | FPS delivery team | Release notes, migration guide, and breaking-change summary published per release tag. |
| Pre-release smoke | Client IT | Run `./tools/validate.sh` equivalent and service smoke checks in a staging environment before applying to production. |
| Apply release | Client IT | Pull updated container images and apply configuration changes per the migration guide. |
| Post-release verification | Client IT | Run smoke checks documented in `docs/production/local-test-harness.md` against the updated environment. |
| Rollback | Client IT | Revert to the previous container images. Dapr component YAML and database schema are designed for backward compatibility within a major version. |

## Client IT Checklist

Before going live with the first production tenant:

- [ ] Container images pulled and verified in a staging environment.
- [ ] All Dapr component YAML files updated for the client environment (state store, pub/sub, secret store, bindings).
- [ ] Secret store provisioned with required secrets; no inline credentials in component YAML.
- [ ] Client IdP configured with required claims (`tenant_id`, `sub`, roles/groups) and `TenantRoleMapping` configured per service.
- [ ] Auth configuration (`Auth:Authority`, `Auth:Audience`) set via environment variables for each service.
- [ ] MongoDB databases provisioned with collection-per-tenant naming; indexes applied.
- [ ] RabbitMQ or equivalent message broker configured and reachable.
- [ ] `/health` endpoint reachable and returning `Healthy` for each service.
- [ ] OpenTelemetry OTLP export configured and traces/metrics visible in the client monitoring platform.
- [ ] Backup schedule configured; restore drill completed and evidenced.
- [ ] HTTPS/TLS certificate applied to external ingress.
- [ ] Dapr mTLS enabled in production component configuration.
- [ ] Demo or synthetic data removed; first real tenant seeded via authorized admin path.
- [ ] GDPR data residency confirmed: all data services in the required region.
- [ ] Incident and rollback runbooks reviewed and accessible to the operations team.
- [ ] `./tools/validate.sh` equivalent passed against the current release before go-live.
