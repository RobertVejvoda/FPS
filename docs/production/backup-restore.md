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
| `tools/backup-stack.sh` | Backs up every durable store in one command — MongoDB (`mongodump`), Postgres/DataHub and Keycloak Postgres (`pg_dump`), MinIO (volume tar), and Vault (native `raft snapshot`). Writes a timestamped directory with a `SHA256SUMS` integrity file and `manifest.json`, and prunes runs beyond `--retention`. Credentials are read from each container's own environment (or, for the Vault token, a single key parsed from `--env-file`) — none touch the host or the repo. `--quiesce` stops the app writers around the dumps for a consistent snapshot. |
| `tools/restore-drill.sh` | Verifies a backup's checksums, tears the stack down **with its volumes**, restores the data stores (Mongo, Postgres, MinIO), performs the guarded NAS Vault restore, brings the stack back up, runs the hosted smoke, and asserts data actually returned. Destructive: requires `--yes`, targets the local stack by default, and refuses `--nas` without `--force-nas`. |

Output goes to a git-ignored `./backups/` directory. Vault artifacts are
**sensitive** (they contain encrypted secret material) and must be handled as
Confidential/Secret per the table above.

**Mongo consistency:** the dump is a per-collection logical snapshot (not a
global point-in-time — `--oplog` is incompatible with the admin/config exclude
the restore needs). Run `backup-stack.sh --quiesce` on a busy instance, or back
up in a low-write window, for a consistent snapshot.

**Vault:** only the native raft snapshot is a valid recovery backup. In `--nas`
mode `backup-stack.sh` **fails closed** if it cannot take one (it never tars a
live raft directory, which would be inconsistent). The restore drill performs
the raft-snapshot restore + re-unseal in `--nas --force-nas` mode when
`VAULT_TOKEN` and `VAULT_UNSEAL_KEYS` are supplied; if a Vault snapshot is
present but not restored, the drill reports **INCOMPLETE** (exit 2) rather than
claiming full recovery. A local dev Vault holds no durable secrets.

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
