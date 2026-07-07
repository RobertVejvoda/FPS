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
| `tools/backup-stack.sh` | Backs up every durable store in one command — MongoDB (`mongodump`), Postgres/DataHub and Keycloak Postgres (`pg_dump`), MinIO (volume tar), and Vault (native `raft snapshot`). Writes a timestamped directory with a `SHA256SUMS` integrity file and `manifest.json`, and prunes runs beyond `--retention`. Credentials are read from each container's own environment (or, for the Vault token, a single key parsed from `--env-file`) — none touch the host or the repo. `--quiesce` stops the writers (app services + Keycloak) around the dumps for a consistent snapshot. |
| `tools/restore-drill.sh` | Verifies a backup's checksums, tears the stack down **with its volumes**, restores the **data / object / identity** stores (Mongo, Postgres, Keycloak Postgres, MinIO), brings the stack back up, runs the hosted smoke, and asserts data actually returned. Vault secret-store restore is a **declared manual step** (below), not automated. Destructive: requires `--yes`, targets the local stack by default, and refuses `--nas` without `--force-nas`. |

Output goes to a git-ignored `./backups/` directory. Vault artifacts are
**sensitive** (they contain encrypted secret material) and must be handled as
Confidential/Secret per the table above.

**Scope of the automated drill:** it reconstructs the durable **data, object,
and identity** stores from backups and proves data returned. Vault secret-store
recovery is a separate **manual DR runbook step** (see below) — so the drill is
not a fully-automated all-store restore, by design.

**Mongo consistency:** the dump is a per-collection logical snapshot (not a
global point-in-time — `--oplog` is incompatible with the admin/config exclude
the restore needs). Run `backup-stack.sh --quiesce` on a busy instance, or back
up in a low-write window, for a consistent snapshot.

**Vault (backup automated, restore manual):** only the native raft snapshot is a
valid recovery backup. In `--nas` mode `backup-stack.sh` **fails closed** if it
cannot take one (it never tars a live raft directory, which would be
inconsistent). The snapshot needs a `VAULT_TOKEN` carrying the
`sys/storage/raft/snapshot` policy; if the stack's Dapr token is least-privilege,
export a snapshot-capable token for the backup. Restoring a snapshot requires
initialising and unsealing a fresh
server-mode node and then re-unsealing with the snapshot cluster's keys —
unseal keys are split-knowledge secrets that must not be handled by an
unattended script, so the restore is a **human-supervised runbook step**
(private `fairspot-platform`, #684). The drill prints the exact sequence when a
Vault snapshot is present. A local dev Vault holds no durable secrets.

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
