# Backup And Restore

Backups are only useful when restore has been tested. FPS production readiness requires both backup automation and documented restore evidence.

## Backup Scope

| Asset | Backup requirement | Notes |
| --- | --- | --- |
| Service-owned data stores | Scheduled snapshots plus point-in-time or equivalent recovery where the chosen hosting option supports it. | Tenant data is isolated by tenant-safe collections, partitions, or keys inside service-owned stores. |
| Tenant storage indexes/metadata | Reproducible provisioning scripts or migrations. | Index and partition definitions must be recoverable without relying only on data snapshots. |
| Dapr pub/sub configuration | Infrastructure as code or versioned component manifests. | Queued messages are operational data; durable delivery should survive broker/provider restart where configured. |
| Dapr component manifests | Versioned configuration in repository or deployment artifact. | Do not store secret values in manifests. |
| Identity provider configuration | Exported configuration or infrastructure-managed setup. | Secrets and signing keys are handled separately. |
| Secret store metadata | Backup according to selected secret-store product. | Secret values require stricter access and recovery controls. |
| Object storage | Versioning or scheduled backup for reports, exports, and future attachments. | Tenant-scoped paths are required for tenant-specific restore. |
| CI/CD and deployment config | Repository history and protected environment settings. | Environment secrets must not be recoverable from Git. |
| Observability config | Dashboards, alert rules, and log retention policies. | Dashboards and alerts should be versioned where practical. |

## Restore Order

1. Confirm incident scope and freeze affected writes if needed.
2. Restore infrastructure and networking baseline.
3. Restore secret store or re-create required secrets through approved rotation.
4. Restore identity provider configuration.
5. Restore service-owned data stores and tenant storage scopes.
6. Restore object storage artifacts needed by the tenant or environment.
7. Restore Dapr component configuration and broker connectivity.
8. Deploy services from known-good images.
9. Validate health checks, login, booking read/write, notification consumption, audit ingestion, and reporting projections.
10. Record restore evidence, data-loss window, and follow-up actions.

## Tenant-Scoped Restore

Because FPS uses tenant-scoped storage boundaries inside service-owned stores, tenant-scoped restore must be planned explicitly. A targeted restore should restore only the affected tenant collections, partitions, keys, and indexes where possible. If the selected managed store cannot restore an individual tenant scope safely, the runbook must restore to a temporary store first, validate the data, then copy only the approved tenant scope back.

Tenant-scoped restore must preserve:

- tenant storage naming and sanitisation rules;
- indexes and unique constraints;
- audit evidence and pseudonymised actor mappings where legally allowed;
- Booking, Notification, Audit, Reporting, Profile, Configuration, and Customer consistency for the same recovery point.

## Restore Evidence

Every restore drill should record:

- date, environment, actor, and approver;
- backup source and recovery point;
- affected services and tenants;
- commands or workflow used;
- validation checks performed;
- actual recovery time and data-loss window;
- defects found and follow-up issues.

## Open Decisions

- Choose snapshot frequency and retention once `OPS000` selects the hosting path.
- Decide whether point-in-time recovery is required for the first hosted pilot.
- Define per-tenant restore tooling after tenant provisioning is implemented.
- Define backup encryption key ownership and recovery process.
