# CUST008 Tenant Onboarding E2E Evidence

This document records the end-to-end onboarding path for one synthetic company in the FairSpot local demo environment. It classifies each step by implementation status and lists the remaining gaps that must be resolved before a first real customer can be onboarded.

**Synthetic company:** ACME Corp  
**Local tenant ID:** `demo` (controlled by `FPS_DEMO_TENANT_ID`)  
**Personas used:** `tenant-admin`, `employee1`, `auditor` (all defined in `docs/production/tenant-onboarding-smoke.md`)

**Related runbook:** `docs/production/tenant-onboarding-smoke.md`  
**Smoke script:** `tools/smoke-onboarding.sh`

---

## Classification Legend

| Symbol | Meaning |
|--------|---------|
| ✅ **Automated** | Exercised by the smoke script or a repeatable API call with no manual steps. |
| 🔧 **Manual** | Requires a human operator step (e.g., Keycloak UI, admin API call not yet scripted). |
| 🟡 **Evaluation-grade** | Works in the local demo via seed/fixture; real-customer path requires further implementation. |
| 🟠 **Pilot-deferred** | Readiness check reports this as `Deferred`; non-blocking for the pilot but must be resolved before production. |
| ❌ **Production gap** | Not implemented; blocks going live with a real customer. |

---

## Onboarding Path — ACME Corp

### Step 1 — Create Tenant Workspace

| Item | Status | Evidence |
|------|--------|---------|
| Tenant record exists (`GET /tenants/demo`) | ✅ Automated | Smoke script step 1; seeded on Customer service startup |
| Lifecycle state transitions (`Draft` → `Configured` → `Seeded`) | ✅ Automated (API) | `POST /tenants/{id}/transitions` implemented; lifecycle enforced by `TenantWorkspace.TryTransition` |
| Tenant creation via web UI | ❌ Production gap | No web form; `POST /tenants` API is implemented. Follow-up: UX track |
| Tenant provisioning metadata for dependent services | 🟡 Evaluation-grade | Services seed their own demo state on startup; no explicit cross-service provisioning call |

**Automation:** `./tools/dev-auth.sh tenant-admin` + `GET http://localhost:5181/tenants/demo`

---

### Step 2 — Configure Identity and Role Mapping

| Item | Status | Evidence |
|------|--------|---------|
| Keycloak realm with `fps-local` client and demo users | 🟡 Evaluation-grade | `code/infrastructure/keycloak/fps-local-realm.json` seeded on startup |
| Token carries `tenantId=demo` and FairSpot roles | ✅ Automated | `GET /me` → `{ tenantId, roles }` verified by smoke script step 2 |
| Role mapping covers `employee`, `admin`, `hr_manager`, `report_viewer`, `auditor` | ✅ Automated | `TenantReadinessService.CheckRoleMappingAsync` validates against `KnownFpsRoles` |
| Per-tenant OIDC client configuration for a real IdP | 🔧 Manual | No IdP configuration UI; requires Keycloak admin or provider-specific configuration |
| Group-to-role mapping via admin API | 🟡 Evaluation-grade | `TenantRoleMapping` seeded in Customer startup; no web form |

**Blocker for real onboarding:** IdP configuration UI and per-tenant group-to-role mapping workflow.

---

### Step 3 — Create First Administrator

| Item | Status | Evidence |
|------|--------|---------|
| `tenant-admin` user carries `admin` role | ✅ Automated | `GET /me` verified by smoke script step 3 |
| `ActiveAdmin` readiness check passes | ✅ Automated | `TenantReadinessService.CheckAdminAsync` |
| First-admin provisioning via `POST /tenants/{id}/identity/admins` | 🟡 Evaluation-grade | API implemented (`TenantIdentityController`); not exercised in smoke (Keycloak user is pre-configured) |
| First-admin creation web UI | ❌ Production gap | No web form. Follow-up: CUST004 |

---

### Step 4 — Configure Parking Operations

| Item | Status | Evidence |
|------|--------|---------|
| Default parking policy exists | ✅ Automated | `GET /configuration/parking-policy` verified by smoke script step 4 |
| `ParkingPolicy` and `ParkingLocation` readiness checks pass | ✅ Automated | `TenantReadinessService` |
| Location and slot data visible to tenant admin | ✅ Automated | `GET /configuration/parking-policy` |
| Tenant admin web UI for location/slot/policy setup | ❌ Production gap | No web form; Configuration API is implemented. Follow-up: UX001 |

---

### Step 5 — Tenant Object Storage

| Item | Status | Evidence |
|------|--------|---------|
| Object storage provisioning (MinIO bucket/prefix) | 🟠 Pilot-deferred | `ObjectStorageReadiness=Deferred` in readiness response (CUST011A). Follow-up: OPS008C |
| Document upload catalog | 🟠 Pilot-deferred | Not implemented. Follow-up: CUST009 |
| Audit evidence export to object storage | 🟠 Pilot-deferred | Audit records exist in-memory; export path not implemented |
| Report export to object storage | 🟠 Pilot-deferred | Reporting service uses in-memory store; export path not implemented |

**Pilot behavior:** The readiness check reports `ObjectStorageReadiness` as `Deferred` with an explicit rationale. No storage paths or bucket names are exposed to tenant admins.

---

### Step 6 — Organization Branding

| Item | Status | Evidence |
|------|--------|---------|
| Branding configuration API (`PUT /tenants/{id}/branding`) | 🟡 Evaluation-grade | API endpoint exists in `TenantController`; no web form |
| Branding asset upload and client-config loading | 🟠 Pilot-deferred | `BrandingReadiness=Deferred` in readiness response (CUST011A). Follow-up: CUST010 |
| Clients load branding through FairSpot APIs (not direct storage URLs) | 🟠 Pilot-deferred | Client-config endpoint not implemented |

**Pilot behavior:** FairSpot defaults (name, generic styling) are used. No bucket names or object keys are exposed.

---

### Step 7 — Load Employee and Profile Facts

| Item | Status | Evidence |
|------|--------|---------|
| `GET /profile/snapshot` returns `parkingEligible=True` for employee1 | ✅ Automated | Smoke script step 6; profile seeded by `dev-seed.sh` |
| HR import CSV templates validate | ✅ Automated | `validate-hr-import.sh` called by smoke script |
| Profile bootstrap API (`POST /profile/bootstrap`) | ✅ Automated (API) | Used by `dev-seed.sh` for `employee1`, `employee2`, `employee3` |
| Web-based HR import upload | ❌ Production gap | No web form. Follow-up: DATA002 |
| SCIM or IdP-claims-driven profile lifecycle | ❌ Production gap | Not implemented. Follow-up: ID002 |

---

### Step 8 — Readiness Check

| Item | Status | Evidence |
|------|--------|---------|
| `GET /tenants/{id}/readiness` returns `isReady=true` | ✅ Automated | Smoke script step 7; `TenantReadinessService` checks all required items |
| Identity, admin, role mapping, parking policy, parking location checks pass | ✅ Automated | Individual check results in readiness response |
| Object storage readiness reported as `Deferred` | 🟠 Pilot-deferred | `ObjectStorageReadiness` check in readiness response |
| Branding readiness reported as `Deferred` | 🟠 Pilot-deferred | `BrandingReadiness` check in readiness response |
| Out-of-process service health probes (Profile, Booking, Notification, Audit, Reporting) | ✅ Automated (HTTP health) | `HttpServiceReadinessProbe` implementations |
| Dry-run mode (`?dryRun=true`) | ✅ Automated (API) | Supported by `TenantReadinessController` |

**Expected readiness response shape (local demo, all services running):**

```json
{
  "tenantId": "demo",
  "isDryRun": false,
  "isReady": true,
  "checks": [
    { "name": "LifecycleState",          "status": "Passed",   "reason": null },
    { "name": "IdentityConfig",          "status": "Passed",   "reason": null },
    { "name": "ActiveAdmin",             "status": "Passed",   "reason": null },
    { "name": "RoleMapping",             "status": "Passed",   "reason": null },
    { "name": "ParkingPolicy",           "status": "Passed",   "reason": null },
    { "name": "ParkingLocation",         "status": "Passed",   "reason": null },
    { "name": "ObjectStorageReadiness",  "status": "Deferred", "reason": "Pilot limitation: Object storage provisioning is not yet implemented. ..." },
    { "name": "BrandingReadiness",       "status": "Deferred", "reason": "Pilot limitation: Organization branding is not configured. ..." },
    { "name": "ProfileFacts",            "status": "Passed",   "reason": null },
    { "name": "BookingSmokeTest",        "status": "Passed",   "reason": null },
    { "name": "NotificationReachable",   "status": "Passed",   "reason": null },
    { "name": "AuditEvidence",           "status": "Passed",   "reason": null },
    { "name": "ReportingEvidence",       "status": "Passed",   "reason": null }
  ]
}
```

---

### Step 9 — First Employee Booking Smoke

| Item | Status | Evidence |
|------|--------|---------|
| `POST /bookings` for employee1 returns `Pending` status | ✅ Automated | Smoke script step 8 |
| `GET /bookings` returns at least 1 booking | ✅ Automated | Smoke script step 8 |
| Booking uses authenticated tenant/user context (no caller-supplied tenant ID) | ✅ Automated | Enforced by Booking service JWT middleware |

---

### Step 10 — Audit Evidence

| Item | Status | Evidence |
|------|--------|---------|
| `GET /audit` returns records (accessed by `auditor` role) | ✅ Automated | Smoke script step 9 |
| Audit records cover tenant setup and booking events | 🟡 Evaluation-grade | In-memory audit store; records exist for the demo session only |
| Persistent, tenant-scoped audit store | ❌ Production gap | Audit service uses in-memory repository. Follow-up: DATA010 track |

---

## Summary: Step Classification

| Step | Description | Status | Pilot-ready? |
|------|-------------|--------|-------------|
| 1 | Tenant workspace | 🟡 Evaluation-grade | Yes (seeded) |
| 2 | Identity and role mapping | 🟡 Evaluation-grade + 🔧 Manual | Yes (local Keycloak) |
| 3 | First administrator | 🟡 Evaluation-grade | Yes (Keycloak pre-configured) |
| 4 | Parking bootstrap | 🟡 Evaluation-grade | Yes (dev-seed.sh) |
| 5 | Tenant object storage | 🟠 Pilot-deferred | Yes (deferred; non-blocking) |
| 6 | Organization branding | 🟠 Pilot-deferred | Yes (defaults used) |
| 7 | Employee/profile bootstrap | 🟡 Eval + ✅ API | Yes (dev-seed.sh + API) |
| 8 | Readiness check | ✅ Automated | Yes |
| 9 | First employee booking | ✅ Automated | Yes |
| 10 | Audit evidence | 🟡 Evaluation-grade | Yes (in-memory) |

**Pilot verdict:** All steps complete for a local demo tenant. The two deferred items (object storage, branding) are explicitly shown in the readiness response and do not prevent day-to-day parking operations.

---

## Production Gaps

The following gaps must be resolved before a first real customer can be onboarded. Each gap blocks the named capability.

| Gap | Capability blocked | Follow-up |
|-----|--------------------|-----------|
| Tenant creation web form | Self-service tenant provisioning | UX track |
| Per-tenant IdP configuration UI | Real SSO onboarding without manual Keycloak config | CUST004 |
| First-admin provisioning API (web form) | Self-service admin creation | CUST004 |
| Parking admin web UI (location/slot/policy) | Self-service parking setup | UX001 |
| Web-based HR import upload | Self-service employee bootstrap | DATA002 |
| Tenant object storage provisioning | Document uploads, exports, audit evidence, branding | OPS008C |
| Controlled document upload catalog | Policy PDFs, consent notices, import evidence | CUST009 |
| Organization branding upload and client-config | Custom logo and color tokens | CUST010 |
| Persistent audit store | Audit records that survive service restart | DATA010 track |
| Persistent notification store | Notifications that survive service restart | DATA010 track |
| Persistent profile store | Profiles that survive service restart | DATA010 track |
| Persistent reporting store | Reports that survive service restart | DATA010 track |
| Persistent configuration store | Policy/slots that survive service restart | DATA010 track |

---

## How to Run the Smoke Script

```bash
# 1. Start the local infrastructure
docker compose -f code/infrastructure/docker-compose.yaml up -d

# 2. Start all FairSpot services
./tools/start-local-harness.sh

# 3. Seed demo data
./tools/dev-seed.sh

# 4. Run the onboarding smoke
./tools/smoke-onboarding.sh
```

Expected output (all services running, demo data seeded):

```
=== Pre-conditions: service health ===
  PASS     Identity health: Healthy
  PASS     Booking health: Healthy
  ...

=== Step 1 — Tenant workspace ===
  PASS     GET /tenants/demo: tenantId=demo lifecycleState=Seeded
  SKIP     Tenant workspace created via POST /tenants ...

...

=== Step 7 — Readiness check ===
  PASS     Readiness check: isReady=True (failed: none)
  DEFERRED Pilot-deferred checks reported by readiness: ObjectStorageReadiness,BrandingReadiness

...

=== Onboarding Smoke Summary ===
All automated checks passed.
2 pilot-deferred item(s) reported. These are non-blocking for the pilot but must be resolved before production.
```
