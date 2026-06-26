# OPS008 Production Persistence And Tenant Provisioning Profile

**Status:** Contract and sequencing defined — implementation slices open (PERSIST001–PERSIST006).
**Prepared by:** Claude (FPS Implementer), 2026-06-25
**Tracks:** Issue #228
**Base contract:** [Tenant Storage Contract](./tenant-storage-contract.md)
**ADRs:** ADR-005 (EF Core is not primary), ADR-006 (writes stay service-owned), ADR-007 (tenant isolation cross-phase)

---

## Purpose

This document records the production persistence sequencing plan for FairSpot. It does **not** restate the provider-neutral contract rules (covered in [tenant-storage-contract.md](./tenant-storage-contract.md)) or select a permanent database vendor. It defines:

- which services have production-blocking in-memory stores today;
- the implementation order (PERSIST001–PERSIST006) and why;
- what provisioning evidence is required before each service can carry customer traffic;
- how derived DataHub projections are validated or rebuilt after durable stores are introduced;
- what "done" looks like per slice.

---

## Persistence Gap Summary

| Service | In-memory store(s) | Blocking for production? | PERSIST slice |
|---|---|---|---|
| Configuration | `InMemoryParkingPolicyRepository`, `InMemoryParkingSlotRepository`, `InMemorySlotChangeRepository` | Yes — policy/slots drive booking eligibility | PERSIST001 |
| Profile | `InMemoryProfileRepository`, `InMemoryDeactivatedUserStore` | Yes — eligibility snapshots must survive restart | PERSIST002 |
| Audit | `InMemoryAuditRepository`, `InMemoryPiiMappingRepository`, `InMemoryErasureRequestRepository` | Yes — compliance evidence must be append-only and durable | PERSIST003 |
| Notification | `InMemoryNotificationRepository`, `InMemoryNotificationPreferencesRepository`, `InMemoryHrRosterStore` | Yes — inbox persistence is a user-trust requirement | PERSIST004 |
| DataHub | EF Core + PostgreSQL (already durable); projection rebuild path not documented | Should fix — rebuild/replay evidence needed | PERSIST005 |
| Booking | `InMemoryEmployeeMetricsService` (fairness history) | Should fix before live draws with repeat participants | PERSIST006A |
| Customer | `InMemoryTenantIdentityConfigStore`, `InMemoryTenantRoleMappingStore` (runtime caches, Dapr-hydrated) | Review required — may be restart-safe already | PERSIST006B |
| Reporting | `InMemoryReportingRepository` | Lower priority — DataHub is the durable read-model direction | Deferred to DataHub track |

**`InMemorySimulationClock`** (Booking) and **`InMemoryEmailNotificationSender`** (Notification) are intentional evaluation-grade stubs, not persistence gaps. They are acceptable until OPS014 (hosted Dapr hardening) defines the production notification channel.

---

## Implementation Sequence

The order is driven by service dependency: Configuration is the upstream of all booking eligibility decisions; Profile is upstream of draw allocation; Audit and Notification are independent but compliance-critical; DataHub projections depend on all write-side stores being reliable.

### PERSIST001 — Configuration Durable Policy and Slot Store

**Current gap:** `InMemoryParkingPolicyRepository`, `InMemoryParkingSlotRepository`, `InMemorySlotChangeRepository`. Policy and slot data are lost on service restart. `dev-seed.sh` re-seeds from scratch; a production deployment cannot rely on this.

**Target state:** Tenant-scoped persistent store via Dapr state store or document collection. Configuration service owns its own store; no other service reads it directly.

**Dapr component:** Uses the shared `fps-statestore` component (or a dedicated `fps-configuration-statestore`). Component scoping to `fps-configuration` app ID is required before customer traffic.

**Tenant key pattern (implemented, PR #595):**

| Entity | Key | Value shape | Notes |
|---|---|---|---|
| Policy version list (tenant default) | `config-policy:{tenantId}:tenant-default` | `List<ParkingPolicy>` (newest last) | Structurally distinct from any location key |
| Policy version list (location override) | `config-policy-location:{tenantId}:{locationId}` | `List<ParkingPolicy>` (newest last) | Separate entity type prevents collision with tenant-default key even if `locationId = "default"` |
| Location slot list | `config-slots:{tenantId}:{locationId}` | `List<ParkingSlot>` | Full slot list replaced atomically via `ReplaceLocationSlotsAsync` |
| Slot change log | `config-slotchange:{tenantId}:{locationId}` | `List<SlotChangeRecord>` (newest last, capped at 100) | Append-only change history per location |

**Design decision — aggregate list keys over per-entity keys:**

The original draft listed per-entity keys (`config-slot:{tenantId}:{locationId}:{slotId}`, `config-slotchange:{tenantId}:{locationId}:{changeId}`). The implementation uses aggregate list keys for two reasons:

1. `IParkingSlotRepository.ReplaceLocationSlotsAsync` replaces the entire slot set for a location in one call. Per-slot keys would require a location index key plus a multi-step read-old/delete-old/write-new cycle that cannot be made atomic without Dapr state transactions. The aggregate-key approach provides atomic replacement in a single `SaveStateAsync` call.

2. The `GetHistoryAsync` interface already reads and returns an ordered list; storing the list directly avoids maintaining a separate version counter or index.

**Trade-offs and restore implications:**
- Each list key is a single Dapr document. Slot lists are bounded by the number of slots per location (typically < 200). Policy history is capped at 50 versions; change log at 100 entries. Both limits are enforced on write.
- Restore scope for a tenant is all keys prefixed with `config-*:{tenantId}:`. A targeted restore of a single slot does not require reading individual slot keys — restore replays the full slot list key.
- If per-slot granularity is required in a future release (e.g., per-slot CRUD without replace), add individual `config-slot:{tenantId}:{locationId}:{slotId}` keys alongside or instead, and migrate at that point.

All keys go through `TenantStorageKey.For(...)` (moved to `FPS.SharedKernel.Infrastructure`, PR #595).

**No caller-supplied storage identifiers.** Location and slot IDs used in keys must be service-assigned; the API must validate that route parameters refer to entities already in the authenticated tenant's scope before using them in key construction.

**Provisioning evidence required before customer traffic:**

- [ ] State store component is bound and health-checked at Configuration service startup (`GET /health` passes).
- [ ] Parking policy and slot data visible via `GET /configuration/parking-policy` after service restart (without re-seeding).
- [ ] Tenant isolation verified: policy data seeded for `demo` tenant is not visible to a second synthetic tenant.
- [ ] No configuration store keys are logged or exposed in error responses.

**DataHub implications:** Configuration changes should emit domain events if DataHub projections ever need policy history (e.g., fairness audit requires knowing what policy was active during a draw). If no DataHub projection depends on Configuration today, event emission can be deferred. Document the decision.

**Done criteria:** `GET /configuration/parking-policy` returns the same tenant-scoped data after a cold restart of the Configuration service, without re-running `dev-seed.sh`. Unit tests cover tenant key generation. Smoke check included in `smoke-hosted.sh` configuration section.

---

### PERSIST002 — Profile Durable Employee/Vehicle/Eligibility Store

**Current gap:** `InMemoryProfileRepository` (profile facts, vehicle data, eligibility), `InMemoryDeactivatedUserStore` (shared kernel, used by all services).

**Target state:** Tenant-scoped persistent store via Dapr state store or document collection. Profile service owns profile and vehicle facts. `InMemoryDeactivatedUserStore` must be replaced with a durable equivalent accessible to Profile and other services via service invocation, or a Dapr state key per deactivated user.

**Dependency on PERSIST001:** Profile eligibility (`parkingEligible`) may depend on parking policy settings. Configuration must be durable before Profile to avoid bootstrapping issues on restart.

**Tenant key pattern (implemented, PR #596):**

| Entity | Key | Value shape | Notes |
|---|---|---|---|
| Employee profile + vehicles | `profile:{tenantId}:{userId}` | `UserProfile` (full document) | Vehicles stored as `IReadOnlyList<Vehicle>` inside the profile document — no separate vehicle keys |
| Employee ID uniqueness index | `profile-empidx:{tenantId}:{employeeId}` | `bool` | Fast duplicate check without full scan |
| Tenant user list | `profile-index:{tenantId}:all` | `List<string>` (userIds) | Required for `ListByTenantAsync`; Dapr has no prefix scan |
| Deactivated user | `deactivated:{tenantId}:{userId}` | `bool` | Stored in `deactivatedstore` component (no scope restriction — shared by all fps-* services) |

**Design decision — vehicles embedded in profile document:**

The OPS008 draft listed per-vehicle keys (`vehicle:{tenantId}:{userId}:{vehicleId}`). The implementation uses `profile:{tenantId}:{userId}` to store the full `UserProfile` document including the `IReadOnlyList<Vehicle>` field. Reasons:

1. `IProfileRepository.SaveAsync` takes a full `UserProfile`. The vehicle list is always updated as part of the profile — there is no per-vehicle update path at the repository interface level.
2. A per-vehicle key design would require a secondary vehicle index per user, plus a read-old/diff/write-new cycle on every profile save. The aggregate-document approach keeps saves atomic.

Restore scope: all profile data for a tenant is under `profile:{tenantId}:*`. Vehicle history is not separately addressable (add per-vehicle keys in a future slice if per-vehicle CRUD is required).

**Design decision — `deactivatedstore` shared Dapr component:**

`deactivated:{tenantId}:{userId}` keys are stored in a dedicated `deactivatedstore` Dapr component with **no scope restriction**. This allows all fps-* services to read and write deactivated-user state from the same backing store. Profile writes on `Deactivate`/`Reactivate`; all other services read during claims transformation. The store uses a 30-second TTL in-process cache to bound Dapr read frequency while limiting cross-instance staleness to ≤30 seconds.

**Provisioning evidence required before customer traffic:**

- [ ] Profile snapshot (`GET /profile/snapshot`) returns same `parkingEligible` value after service restart.
- [ ] Deactivated user status survives restart (deactivation must block new token requests).
- [ ] Profile store health-checked at startup.
- [ ] Tenant isolation verified: employee profile seeded for `demo` tenant is not visible to a second synthetic tenant.

**DataHub implications:** `ProfileFact.Updated` events already flow to DataHub if subscribed. After PERSIST002, the Profile service emits events on durable writes; DataHub can rebuild employee snapshots from event replay. Confirm that DataHub's `BookingProjectionHandler` uses the stable `userId`/`tenantId` combination consistent with the durable profile store.

**Done criteria:** `GET /profile/snapshot` returns same data after cold Profile service restart without re-seeding. Deactivated user state blocks token validation after restart.

---

### PERSIST003 — Audit Durable Append-Only Evidence Store and PII Mapping

**Current gap:** `InMemoryAuditRepository`, `InMemoryPiiMappingRepository`, `InMemoryErasureRequestRepository`. Audit records are lost on service restart — a compliance blocker.

**Target state:** Append-only tenant-scoped collection for audit records (no DELETE path on audit records). Separate collection/partition for PII mapping to support GDPR erasure without deleting pseudonymised audit records. Erasure requests stored in a separate, durable, time-bounded store.

**Append-only constraint:** The store implementation must not expose a delete or update operation on audit records to the application layer. GDPR erasure operates on the PII mapping table (delete the actor-hash-to-name mapping), leaving audit records intact but effectively pseudonymised. This must be enforced at the repository interface level (`IAuditRepository` must have no `Delete` or `Update` method for audit records).

**Tenant key pattern and component/collection separation (implemented, PR #597):**

Three physically separate Dapr components back the three stores, all pointing to the `fps-audit` MongoDB database but to independent collections. This allows MongoDB-level backup, restore, and TTL index policies per collection, and ensures a GDPR erasure purge of `pii-mappings` cannot touch `auditlog`.

| Entity | Dapr component | MongoDB collection | Key pattern | Value shape | Notes |
|---|---|---|---|---|---|
| Audit record | `auditstore` | `auditlog` | `audit:{tenantId}:{recordId}` | `AuditRecord` | Append-only; no delete via repo |
| Tenant audit index | `auditstore` | `auditlog` | `audit-index:{tenantId}:all` | `List<string>` (recordIds) | Required for tenant-scoped listing (no prefix scan in Dapr) |
| Source-event idempotency | `auditstore` | `auditlog` | `audit-src:{tenantId}:{sourceEventId}` | `bool` | **Tenant-scoped**: same sourceEventId in two tenants creates two independent audit records. Marker written after the record is visible in the index (write order: record → index → marker). |
| PII mapping (userId → hash) | `pii-mappingstore` | `pii-mappings` | `pii:{tenantId}:{userId}` | `PiiMapping` | Separate Dapr component; GDPR erasure can truncate/drop without touching auditlog |
| PII hash reverse index | `pii-mappingstore` | `pii-mappings` | `pii-hash:{tenantId}:{actorHash}` | `string` (userId) | Bidirectional lookup: hash → userId for batch resolution |
| Erasure request | `erasure-store` | `erasure-requests` | `erasure:{tenantId}:{erasureRequestId}` | `ErasureRequest` | Separate Dapr component; lifecycle mutations (status, serviceResults) do not touch auditlog |

All keys pass through `TenantStorageKey.For(...)` (in `FPS.SharedKernel.Infrastructure`). Tenant ID comes from the JWT claim only — no caller-supplied storage identifiers.

**Dapr component files:**
- `code/infrastructure/dapr/components/demo/auditstore.yaml` — MongoDB `fps-audit.auditlog`
- `code/infrastructure/dapr/components/demo/pii-mappingstore.yaml` — MongoDB `fps-audit.pii-mappings`
- `code/infrastructure/dapr/components/demo/erasure-store.yaml` — MongoDB `fps-audit.erasure-requests`
- Smoke equivalents use `state.in-memory` with the same component names.

**Indexes required:** `tenantId`, `occurredAt`, `action`, `actorHash` on `auditlog`. `actorHash` on `pii-mappings` (supports efficient batch lookup for auditor workspace). No index on `erasure-requests` beyond the primary key — expected volume is low.

**Provisioning evidence required before customer traffic:**

- [ ] Audit records visible via `GET /audit` after service restart (existing records must survive restart).
- [ ] PII mapping stored in `pii-mappingstore` / `pii-mappings` collection — independently addressable from audit records.
- [ ] Erasure request stored in `erasure-store` / `erasure-requests` collection.
- [ ] Erasure request workflow updates PII mapping without modifying audit records.
- [ ] Append-only contract documented and enforced at the repository interface.
- [ ] Tenant isolation verified: `demo` tenant audit records are not visible from a second tenant's API calls.
- [ ] Cross-tenant idempotency: same `sourceEventId` in two tenants produces two independent audit records (regression test in `DaprAuditRepositoryTests.AppendAsync_SameSourceEventId_DifferentTenants_BothAccepted`).

**DataHub implications:** Audit events may feed a DataHub compliance projection. With a durable audit store, projection rebuild can replay events from the store (via `GET /audit` paginated endpoint) rather than requiring broker replay. Document the rebuild path.

**Done criteria:** 7+ audit records persist across cold restart without re-seeding. GDPR erasure of an actor hash does not delete any audit records.

---

### PERSIST004 — Notification Durable Inbox, Preferences, and HR Audience State

**Current gap:** `InMemoryNotificationRepository` (inbox), `InMemoryNotificationPreferencesRepository` (delivery preferences), `InMemoryHrRosterStore` (HR audience routing).

**Target state:** Tenant-scoped persistent store. Notifications are user-visible UX features; employees expect their inbox to persist across sessions.

**Tenant key pattern:**

| Entity | Key pattern |
|---|---|
| Notification record | `notification:{tenantId}:{recipientId}:{notificationId}` |
| User preferences | `notif-prefs:{tenantId}:{recipientId}` |
| HR roster entry | `notif-roster:{tenantId}:{userId}` |

**Retention:** 90 days after creation (see [Tenant Storage Contract](./tenant-storage-contract.md#notification)).

**Provisioning evidence required before customer traffic:**

- [ ] Notification records visible via `GET /notifications` after service restart without re-triggering booking events.
- [ ] Notification service health check passes on startup (store accessible).
- [ ] `totalReturned` field returned in paginated response (confirmed compatible with `smoke-hosted.sh` `json_list_len`).
- [ ] Tenant isolation verified: notifications seeded for `demo` tenant are not visible to a second tenant.

**DataHub implications:** Notification delivery evidence (delivered/failed/read) is a candidate DataHub projection for HR reporting on communication reach. No DataHub subscription to notification events exists today. If added, it follows the standard Dapr pub/sub topic pattern and must use tenant-scoped projection keys.

**Done criteria:** `GET /notifications` returns the same records after cold Notification service restart. Mandatory check #6 in `smoke-hosted.sh` passes without re-running `dev-seed.sh`.

---

### PERSIST005 — DataHub Projection Durability and Rebuild Evidence

**Current state:** DataHub already uses EF Core + PostgreSQL (`DataHubDbContext`). The `BookingProjectionHandler` processes booking events from Dapr pub/sub and writes to the durable store. DataHub is not in-memory.

**Gap:** The rebuild/replay path when the PostgreSQL store is reset or when projections need to be re-derived from upstream events is not documented. There is no evidence that projections can be rebuilt from event replay after a failure.

**Target state:** Document the rebuild path and test it for the existing `BookingProjectionHandler`.

**Rebuild expectations:**

1. **Incremental mode (normal operation):** DataHub subscribes to Dapr topics; new events are appended to projections via `EventInboxService` → `BookingProjectionHandler`. No replay needed under normal operation.

2. **Cold rebuild from events (after store reset):** If the DataHub PostgreSQL store is reset or a new projection type is introduced:
   - The source service (e.g., Booking) must either replay events via a replay API endpoint, or the event broker must support per-subscription offset reset.
   - DataHub processes replayed events idempotently (event ID deduplication in `EventInboxService`).
   - Replay scope must be tenant-scoped to avoid cross-tenant data leakage during rebuild.

3. **Snapshot rebuild from source service API (alternative):** DataHub can poll `GET /bookings` (paginated) for a tenant to rebuild the booking projection from the durable Booking store, instead of relying on event replay. This requires PERSIST001–PERSIST004 to be complete so source stores are reliable.

**Provisioning evidence required:**

- [ ] DataHub PostgreSQL connection string and EF Core migrations applied before service startup.
- [ ] `EventInboxService` deduplication prevents duplicate projection writes on event re-delivery.
- [ ] Cold rebuild documented: either event replay or snapshot poll path is confirmed working for at least one projection type (`BookingProjectionHandler`).
- [ ] Tenant-scoped projection rows in `DataHubDbContext` tables (confirm `tenantId` column exists on projection entities).

**Done criteria:** DataHub booking projection survives PostgreSQL schema migration without data loss. Rebuild path documented with a manual test procedure. Projection rows confirm tenant-scoped isolation.

---

### PERSIST006A — Booking Fairness Metrics Durability

**Current gap:** `InMemoryEmployeeMetricsService` tracks fairness history (allocation counts per employee per lookback window). This history is lost on Booking service restart. If fairness metrics are erased, the draw algorithm may over-allocate to employees who received spots recently.

**Target state:** Dapr state store key per employee per tenant, with consistent key format.

**Tenant key pattern:**

| Entity | Key pattern |
|---|---|
| Employee allocation count | `metrics:{tenantId}:{userId}:{period}` |

**Note:** If the production draw cadence makes stale restart-loss acceptable (e.g., weekly draws where the lookback window is longer than the expected restart interval), this gap may be accepted with documented rationale. The implementer must confirm the risk with the product owner before deferring.

**Done criteria:** Employee fairness metrics visible to `DrawService` after cold Booking service restart. Lookback window calculation produces consistent results across restarts.

---

### PERSIST006B — Customer Identity Runtime Cache Review

**Current state:** `InMemoryTenantIdentityConfigStore` and `InMemoryTenantRoleMappingStore` are registered in Customer `Program.cs` and used at runtime for JWT claim transformation. However, they are hydrated from the durable `DaprCustomerIdentityRepository` at startup via `HydrateIdentityStoresAsync()`.

**Review required:**

1. Confirm that no runtime mutation of these stores occurs without a corresponding write to the durable Dapr store. If an admin updates identity config at runtime, the in-memory store must be updated AND the Dapr store must be written atomically (or the service must be designed so the Dapr store is always the write target, with the in-memory store as a read cache only).

2. Confirm that `HydrateIdentityStoresAsync()` runs before the service accepts traffic (startup gate, not background task).

3. If runtime mutations are possible without Dapr persistence, add a write-through path before this gap is closed.

**Done criteria:** Documentation in `TenantIdentityService` (or a README section) confirms whether the in-memory stores are pure read caches or whether they accept unperisted mutations. If mutations bypass Dapr, add write-through before marking done.

---

## DataHub Read Model Rebuild — Cross-Slice Expectations

When any service in PERSIST001–PERSIST004 gains a durable store, DataHub projections that were seeded by in-memory events during the demo session must be evaluated:

| Source service | DataHub projection | Rebuild path after durable store added |
|---|---|---|
| Booking | `BookingProjectionHandler` (already active) | Event replay via Dapr broker offset reset, or snapshot poll from `GET /bookings` paginated endpoint |
| Configuration | None defined yet | If added: snapshot poll from `GET /configuration/parking-policy` |
| Profile | None defined yet | If added: snapshot poll from `GET /profile/snapshot` per known user list |
| Audit | None defined yet | If added: event replay or paginated poll from `GET /audit` |
| Notification | None defined yet | If added: not expected; notification delivery evidence is aggregated, not projected per-message |

**Tenant isolation rule for rebuilds:** Any rebuild or replay operation must be scoped to a single tenant. A multi-tenant rebuild must iterate tenant-by-tenant. DataHub must not issue a cross-tenant projection query against source service APIs.

**DataHub projection rebuild is not in scope for PERSIST001–PERSIST004 implementation PRs.** Each PERSIST slice documents what DataHub implications exist; a dedicated DataHub hardening slice (PERSIST005) confirms the rebuild path.

---

## Provisioning Checklist

Use this checklist when adding a durable store to any service. All items must be satisfied before the service can carry customer traffic.

```
[ ] Service has no in-memory repository registered as the primary persistence implementation.
[ ] Dapr state store component (or equivalent) is bound in the service's component manifest.
[ ] Component is scoped to the service's Dapr app ID (not global).
[ ] Tenant storage keys use TenantStorageKey.For(...) or equivalent shared sanitisation helper.
[ ] No caller-supplied storage identifiers are used in key construction (tenant comes from JWT claim only).
[ ] Health check at service startup confirms store accessibility before traffic is accepted.
[ ] GET endpoint for the primary entity type returns the same data after cold service restart without re-seeding.
[ ] Tenant isolation verified: data seeded for one tenant is not visible to a second tenant's API.
[ ] Secret references use Dapr secretstoreref pattern (no inline credentials in component YAML).
[ ] Store name/collection/bucket name does not appear in API error responses, logs, or events.
[ ] Retention category documented (see tenant-storage-contract.md).
[ ] Backup scope documented and confirmed compatible with tenant-scoped restore.
[ ] PERSIST slice issue closed with evidence comment before marking Done on the project board.
```

---

## Secret and Credential Rules

These rules apply to all PERSIST slices.

| Rule | Requirement |
|---|---|
| No inline credentials | Component YAML must use `secretKeyRef` or `secretstoreref`, not plain connection string values |
| No committed credentials | `.env.nas`, `.env.local`, and service-specific env files are gitignored |
| No store names in logs | Collection, bucket, or table names must not appear in application logs or API error responses |
| No storage identifiers in API responses | Object keys, collection names, and connection metadata must not be returned to API callers |
| Rotation path | Each secret must have a documented rotation path that does not require a service restart (or if a restart is required, it must be documented) |

---

## Reconciliation with Current Documentation

| Document | Gap addressed by this document | Action |
|---|---|---|
| `tenant-storage-contract.md` | Base contract and key format rules | No change — this document extends it with implementation sequencing |
| `backup-restore.md` | Per-slice backup scope is defined in the contract; restore order is in backup-restore.md | Add reference to OPS008 in backup-restore.md open decisions |
| `gap-analysis.md` | GAP-001 references persistence work | Update GAP-001 to reference OPS008 and PERSIST001–PERSIST006 |
| `client-production-handoff.md` | Database provisioning responsibility is client IT | No change — client IT still owns the store provisioning; FairSpot supplies the contract |

---

## Follow-Up Implementation Issues

| Issue | Title | Priority |
|---|---|---|
| [#587](https://github.com/RobertVejvoda/fairspot/issues/587) PERSIST001 | Configuration durable policy and slot store | P0 — blocks booking eligibility correctness |
| [#588](https://github.com/RobertVejvoda/fairspot/issues/588) PERSIST002 | Profile durable employee eligibility and deactivated user store | P0 — blocks draw and eligibility correctness |
| [#589](https://github.com/RobertVejvoda/fairspot/issues/589) PERSIST003 | Audit durable append-only evidence store and PII mapping | P0 — compliance blocker |
| [#590](https://github.com/RobertVejvoda/fairspot/issues/590) PERSIST004 | Notification durable inbox, preferences, and HR audience state | P1 — user trust |
| [#591](https://github.com/RobertVejvoda/fairspot/issues/591) PERSIST005 | DataHub projection rebuild path evidence | P1 — operational reliability |
| [#592](https://github.com/RobertVejvoda/fairspot/issues/592) PERSIST006A | Booking fairness metrics durability | P1 — draw fairness correctness |
| [#593](https://github.com/RobertVejvoda/fairspot/issues/593) PERSIST006B | Customer identity runtime cache review | P2 — review and documentation only |

---

## Document Change Log

| Date | Author | Change |
|---|---|---|
| 2026-06-25 | Claude | Initial OPS008 persistence profile — implementation sequence, provisioning evidence, DataHub implications, checklist |
| 2026-06-26 | Claude | PERSIST001 implemented (PR #595) — update key pattern table to match aggregate-list-key design; document trade-offs, bounds, and restore implications |
| 2026-06-26 | Claude | Fix tenant-default/location-override key collision: use `config-policy:{tenantId}:tenant-default` and `config-policy-location:{tenantId}:{locationId}` as structurally distinct prefixes |
