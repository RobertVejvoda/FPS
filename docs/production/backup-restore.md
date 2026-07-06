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

## Automation (open-core scripts)

The repository ships the backup and restore-drill automation; schedules, storage
locations, credentials, and the recorded drill **evidence** stay in the private
`fairspot-platform` runbook (#684).

| Script | Purpose |
| --- | --- |
| `tools/backup-stack.sh` | Backs up every durable store in one command — MongoDB (`mongodump --oplog`), Postgres/DataHub and Keycloak Postgres (`pg_dump`), MinIO (volume tar), and Vault (native `raft snapshot`, volume-tar fallback when sealed). Writes a timestamped directory with a `SHA256SUMS` integrity file and `manifest.json`, and prunes runs beyond `--retention`. Credentials are read from each container's own environment — none touch the host or the repo. |
| `tools/restore-drill.sh` | Verifies a backup's checksums, tears the stack down **with its volumes**, restores every store, brings the stack back up, runs the hosted smoke, and asserts data actually returned. Destructive: requires `--yes`, targets the local stack by default, and refuses `--nas` without `--force-nas`. |

Output goes to a git-ignored `./backups/` directory. Vault artifacts are
**sensitive** (they contain encrypted secret material) and must be handled as
Confidential/Secret per the table above. A Vault raft snapshot is restored on
NAS after init+unseal (`vault operator raft snapshot restore`); it is
intentionally not auto-applied to a local dev Vault, which holds no durable
secrets.

Typical drill:

```bash
./tools/backup-stack.sh                       # produce a backup under ./backups/<stamp>
./tools/restore-drill.sh --from ./backups/<stamp> --yes   # wipe + restore + smoke
```

## Public References

- [RTO/RPO Requirements](./rto-rpo-requirements)
- [Availability Model](./availability-model)
- [Security Architecture](../architecture/security/)
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary)
