# OPS019 NAS Encryption-at-Rest and Backup Evidence

**Status:** Operator checklist — complete and attach before any real customer data is processed.
**Tracks:** Issue #619
**Related:** [encryption.md](../security/encryption.md) (Data At Rest), [gap-register.md](../security/gap-register.md), [nas-cloudflare-deployment-profile.md](./nas-cloudflare-deployment-profile.md), [hosted-smoke-runbook.md](./hosted-smoke-runbook.md) (SEC011 public-boundary gate)

---

## Purpose

FairSpot cannot prove disk encryption from application code alone (see [encryption.md](../security/encryption.md) → Data At Rest). This document is the operator-owned checklist that **records** the encryption-at-rest and backup state of a NAS deployment, so Release 1 can distinguish *local synthetic-data testing* from *hosted customer-data readiness*.

> **Release 1 readiness gate.** A NAS profile may run with **synthetic/demo data** (Green Logistics, seeded demo) without this evidence. It must **not** process **real customer data** until every mandatory row below is `YES` and a restore drill has been evidenced. The SEC011 public-boundary gate ([hosted-smoke-runbook.md](./hosted-smoke-runbook.md)) covers encryption *in transit* and the public boundary; this covers encryption *at rest* and *backup/restore*.

---

## 1. What must be encrypted at rest

All FairSpot durable state lives in Docker **named volumes** on the NAS. On Synology/QNAP these volumes live under a shared folder on a storage volume, so encryption is applied at the **Synology encrypted shared folder** (or full-volume encryption) level — not per container.

These are the durable named volumes actually defined in `code/infrastructure/docker-compose*.yml`:

| Store | Volume | Holds | Mandatory |
|---|---|---|---|
| MongoDB | `mongodb_data` | Booking, Profile, Configuration, Notification, Audit authoritative state | **Yes** |
| PostgreSQL | `postgres_data` | DataHub read-model projections (outcomes, draw history, metrics) | **Yes** |
| Vault | `vault_data` | Secret store (raft storage, Dapr component credentials) | **Yes** |
| MinIO | `minio_data` | Object storage (exports, erasure evidence) | **Yes** |
| Grafana / Prometheus / Loki | `grafana_data`, `prometheus_data`, `loki_data` | Operational metrics/logs (may contain tenant identifiers) | Recommended |
| **All backup targets** | — | Snapshots/exports of every store above | **Yes** |

> **Persistence gap — RabbitMQ and Keycloak have no durable volume today.** The compose files define named volumes only for the stores above. **RabbitMQ has no data-volume mount** (broker/event state is ephemeral), and **Keycloak mounts only its themes directory read-only** (realm/user/session state is not persisted across container recreation). Before real customer data:
> - **Keycloak must be given durable, encrypted state** — a persistent data volume or an external Postgres — otherwise realms and users are lost when the container is recreated.
> - **RabbitMQ should be given a persistent, encrypted volume** (`/var/lib/rabbitmq`) where broker durability is required. With the transactional-outbox pattern, in-flight loss is recoverable, but a durable broker is preferred for a hosted profile.
>
> Adding these volumes is a follow-up task on the service that owns persistence; this checklist **gates them as a production-readiness blocker** (see §4). Once added, they must live on the encrypted shared folder like every other store.

> Recovery keys, Vault unseal shares, and backup-encryption passphrases are **never** committed to GitHub or written to `nas.env`. They live with the operator (password manager / NAS Key Manager / offline). This document records *who owns* them and *that they exist* — never their values.

---

## 2. Encryption-at-rest evidence

Complete one row per store. "Method" = e.g. *Synology encrypted shared folder (AES-256)*, *encrypted storage volume*, *self-encrypting drive*.

| Store / volume | Encrypted at rest? (YES/NO) | Method | Recovery-key owner | Verified by / date |
|---|---|---|---|---|
| MongoDB (`mongodb_data`) | | | | |
| PostgreSQL (`postgres_data`) | | | | |
| Vault (`vault_data`) | | | | |
| MinIO (`minio_data`) | | | | |
| Observability (`prometheus_data`, `loki_data`, `grafana_data`) | | | | |
| Keycloak — persistence gap (§1): add durable encrypted state first | | | | |
| RabbitMQ — persistence gap (§1): add durable encrypted volume if required | | | | |

**How to verify on Synology DSM:** Control Panel → Shared Folder → select the folder holding `/volume*/docker` → confirm **Encryption** is enabled; on the storage volume, confirm volume/drive encryption status. Record the method and the key owner above. Do **not** paste the recovery key.

---

## 3. Backup and restore evidence

| Field | Value |
|---|---|
| Backup target (where) | |
| Backup target encrypted? (YES/NO) | |
| Backup encryption method | |
| Backup schedule (frequency/retention) | |
| Backup-encryption-secret owner | |
| Restore-drill owner | |
| **Latest restore test date** | |
| Restore-drill result (PASS/FAIL + notes) | |

**Restore drill (minimum):** restore the most recent backup of `mongodb_data` + `postgres_data` into a throwaway target, bring the stack up against it, and confirm the SEC011/hosted smoke ([hosted-smoke-runbook.md](./hosted-smoke-runbook.md)) passes against the restored data. Record the date and result above. See [OPS009A restore drill checklist](https://github.com/RobertVejvoda/fairspot/issues/237) for the observability-side drill.

---

## 4. Sign-off

This NAS deployment is cleared for **real customer data** only when:

- [ ] Every **Mandatory: Yes** store in §1 shows `YES` in §2 with a named recovery-key owner.
- [ ] The §1 **persistence gap is resolved**: Keycloak (and RabbitMQ where broker durability is required) have durable, encrypted state — not the ephemeral default.
- [ ] §3 backup target is encrypted, with a named owner and a recorded backup schedule.
- [ ] §3 records a **passed restore drill** within the operator's RPO/RTO window (see [rto-rpo-requirements.md](./rto-rpo-requirements.md)).
- [ ] No recovery key, unseal share, or backup passphrase appears in GitHub, `nas.env`, logs, or smoke evidence.

| Role | Name | Date |
|---|---|---|
| Operator (encryption + backup owner) | | |
| Release 1 readiness sign-off | | |

Until this sign-off is complete, the deployment is **synthetic-data only**.

---

## Document change log

| Date | Author | Change |
|---|---|---|
| 2026-06-28 | Claude | OPS019: initial NAS encryption-at-rest + backup evidence checklist (#619) |
