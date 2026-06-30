# Backup And Restore

> **Moved private (#684):** the detailed hosted-operator backup/restore procedure now lives in the private `fairspot-platform` repository at `docs/runbooks/backup-restore.md`.

This public page records the backup and recovery contract. Provider commands, schedules, credentials, storage locations, restore drill evidence, and operator escalation steps belong in the private runbook or a client-owned operations repository.

## Public Contract

| Area | Requirement |
| --- | --- |
| Scope | Backups cover service-owned state stores, read models, configuration, identity mappings required for operation, object storage, and deployment metadata needed for recovery. |
| Tenant safety | Restore procedures must preserve tenant boundaries. Tenant-scoped restore should restore only the affected tenant scope, or restore to a temporary environment first and copy back the approved tenant data. |
| Encryption | Backup artifacts are encrypted at rest and protected as Confidential or Secret data according to their content. |
| Evidence | Restore drills are recorded with date, scope, result, operator, and follow-up actions. |
| RTO/RPO | Recovery targets are defined in [RTO/RPO Requirements](./rto-rpo-requirements). |
| Ownership | Client-owned deployments may use client backup systems, but they must still satisfy the FairSpot backup/restore contract. |

## Public References

- [RTO/RPO Requirements](./rto-rpo-requirements)
- [Availability Model](./availability-model)
- [Security Architecture](../architecture/security/)
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary)
