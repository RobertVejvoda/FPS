# Tenant Onboarding E2E Smoke Scenario

This document defines the end-to-end smoke scenario for onboarding a synthetic company tenant in FairSpot. Each step is marked with its implementation status for the local demo environment.

**Status legend:**
- ✅ **Implemented** — runnable today via API or script
- 🔧 **Manual** — requires manual steps in a UI or config file
- 🟡 **Evaluation-grade** — exists but uses demo shortcuts not suitable for production
- ❌ **Missing** — not yet implemented; blocker issue noted

**Supported local demo tenant:** `demo` (default). This is the only tenant that works end-to-end in the local harness because the Keycloak realm fixture hardcodes `tenant_id=demo` for all seeded users. The `FPS_DEMO_TENANT_ID` environment variable controls where Customer, Configuration, and profile seed scripts land their data, but Keycloak tokens always carry `demo` from a static realm import — smoke checks that compare the token tenant (e.g. `GET /me → tenantId`) will only pass when `FPS_DEMO_TENANT_ID=demo`. A second tenant (`acme-corp`) can be provisioned via `tools/provision-tenant.sh tools/templates/tenants/acme-corp.json` but its users must be added to Keycloak manually for JWT-bearing smoke steps.

**Synthetic tenant for smoke steps:** `acme-corp` (documentation only), a company with 7 employees, 1 office location (`Prague`), and a limited-capacity parking setup.

**Demo personas (fictional — all data is synthetic):**

| Username | Display name | Role | Demo focus |
| --- | --- | --- | --- |
| `employee1` | Jan Novak | Employee | Standard booking path; two vehicles (sedan + EV) |
| `employee2` | Petra Svobodova | Employee | Company-car booking; fleet vehicle |
| `employee3` | Tomas Dvorak | Employee | Accessibility-eligible booking |
| `hr-admin` | Lucie Prochazkova | HR Manager | Policy, slot management, employee bootstrap |
| `tenant-admin` | Karel Urban | Admin | Tenant setup, readiness, configuration |
| `report-viewer` | Eva Kralova | Report Viewer | Reporting and CSV export |
| `auditor` | Martin Cerny | Auditor | Audit record query and evidence review |

---

## Pre-conditions

1. Local infrastructure running: `docker compose -f code/infrastructure/docker-compose.yaml up -d`
2. Local harness running: `./tools/start-local-harness.sh`
3. Keycloak available at `http://localhost:8180`
4. All service health checks green:
   ```bash
   for port in 5192 5131 5197 5157 5161 5171 5141 5181; do
     status=$(curl -s http://localhost:$port/health | python3 -c "import sys,json; print(json.load(sys.stdin)['status'])" 2>/dev/null || echo "UNREACHABLE")
     echo "  :$port → $status"
   done
   ```

---

## Step 1 — Create Tenant Workspace

**Status:** ✅ Implemented

Customer service exposes `POST /tenants` for tenant creation. The local demo pre-seeds `demo` on startup so the API is exercisable without a UI.

**Verify tenant exists:**
```bash
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5181/tenants/demo | python3 -m json.tool
```

**Expected:** Tenant record with `tenantId=demo`, `slug=demo-company`, lifecycle state `Seeded`.

---

## Step 2 — Configure Identity and Role Mapping

**Status:** 🔧 Manual (Keycloak) + 🟡 Evaluation-grade (role mapping)

The local Keycloak realm (`fps-local`) is imported from `code/infrastructure/keycloak/fps-local-realm.json` which pre-configures the OIDC client, roles, and demo users. This represents step 2 for evaluation.

**Role mapping:** The Customer service seed registers a `TenantRoleMapping` for `demo` that maps Keycloak realm roles directly to FairSpot roles (pass-through). In a real onboarding, this mapping would be configured via an admin API call.

**Verify identity is wired:**
```bash
TOKEN=$(./tools/dev-auth.sh employee1)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/me | python3 -m json.tool
```

**Expected:** `{"userId": "employee1", "tenantId": "demo", "roles": ["employee"]}`

**Blocker for production:** IdP configuration UI and documented per-tenant group-to-role mapping workflow.

---

## Step 3 — Create First Administrator

**Status:** 🟡 Evaluation-grade

`tenant-admin` is pre-configured in Keycloak with the `admin` role for `demo`. This represents the first administrator for evaluation.

**Verify:**
```bash
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/me | python3 -m json.tool
```

**Expected:** `{"userId": "tenant-admin", "tenantId": "demo", "roles": ["admin"]}`

**Blocker for production:** Formal first-admin provisioning path (mapped SSO user or FairSpot-local break-glass account creation via API). Follow-up: CUST004 evidence.

---

## Step 4 — Parking Bootstrap (Location, Policy, Slots)

**Status:** 🟡 Evaluation-grade

The Configuration service seed creates `Prague` with 10 parking slots and a default policy. This represents steps 4 for evaluation.

**Verify:**
```bash
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/configuration/parking-policy | python3 -m json.tool
```

**Expected:** Policy document with slot count, time zone, and effective date.

**Blocker for production:** Tenant admin UI for location/slot/policy setup. Configuration service API is implemented but no web UI exists yet. Follow-up: UX001/web admin surface.

---

## Step 5 — Tenant Object Storage

**Status:** 🟡 Pilot-deferred (readiness evidence added)

Tenant object storage provisioning (document upload, report export, audit evidence export, GDPR export, branding upload) is not yet implemented. The tenant readiness check now reports `ObjectStorageReadiness` as **Deferred** with explicit pilot rationale — this is a non-blocking, visible pilot limitation rather than a silent gap.

The Tenant Admin page shows this as an amber "(pilot deferred)" item in the readiness panel. Resolve before production: OPS008C.

**Verify deferred status appears in readiness response:**
```bash
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5181/tenants/demo/readiness | python3 -m json.tool
```

**Expected:** Readiness response includes `{ "name": "ObjectStorageReadiness", "status": "Deferred", "reason": "Pilot limitation: ..." }`.

**Production blocker:** Tenant object storage provisioning and readiness probe. Follow-up: OPS008C.

---

## Step 6 — Organization Branding Assets

**Status:** 🟡 Pilot-deferred (readiness evidence added)

Tenant branding asset upload and client-config loading are not yet implemented. The tenant readiness check now reports `BrandingReadiness` as **Deferred** with explicit pilot rationale — FairSpot defaults are used during the pilot without exposing bucket names or object keys.

The Tenant Admin page shows this as an amber "(pilot deferred)" item in the readiness panel. Resolve before production: CUST010.

**Verify deferred status appears in readiness response:**
```bash
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5181/tenants/demo/readiness | python3 -m json.tool
```

**Expected:** Readiness response includes `{ "name": "BrandingReadiness", "status": "Deferred", "reason": "Pilot limitation: ..." }`.

**Production blocker:** Tenant branding asset upload/catalog and client-config exposure. Follow-up: CUST010.

---

## Step 7 — Employee and Profile Bootstrap

**Status:** 🟡 Evaluation-grade (seed) / ✅ Implemented (API)

Profile service provides `POST /profile/bootstrap` for seeding employee profiles. The `dev-seed.sh` script calls this for `employee1`, `employee2`, `employee3`.

**Run the employee seed:**
```bash
./tools/dev-seed.sh
```

**Verify profiles exist:**
```bash
TOKEN=$(./tools/dev-auth.sh employee1)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/profile/snapshot | python3 -m json.tool
```

**Expected:** Profile snapshot with parking eligibility, vehicle details (for employee1), and accessibility flags.

**HR import template:** `tools/templates/demo-employees.csv` and `demo-vehicles.csv` define the full employee set matching Keycloak users. Run validation:
```bash
./tools/validate-hr-import.sh tools/templates/demo-employees.csv tools/templates/demo-vehicles.csv
```

**Blocker for production:** Web-based HR import upload (DATA002 in progress). Current path requires manual API calls or the seed script.

---

## Step 8 — Readiness Check

**Status:** ✅ Implemented

The Customer service exposes `GET /tenants/{tenantId}/readiness` which checks identity, policy, profile, booking, notification, audit, and reporting readiness probes.

```bash
TOKEN=$(./tools/dev-auth.sh tenant-admin)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5181/tenants/demo/readiness | python3 -m json.tool
```

**Expected after all previous steps:** status `Ready` or `Configured` with per-probe pass/fail breakdown.

Note: The local demo wires evaluation-grade HTTP health probes for Profile, Booking, Notification, Audit, and Reporting. These probes prove that the dependent services are reachable and healthy before marking the tenant ready. Object-storage (`ObjectStorageReadiness`) and branding (`BrandingReadiness`) checks are now included in the readiness response with a `Deferred` status, making pilot limitations explicit and non-blocking. Deeper tenant-specific evidence checks, such as verifying exact seeded profile counts, storage namespaces, branding assets, or audit rows, remain future hardening.

---

## Step 9 — First Booking Smoke

**Status:** ✅ Implemented

After the tenant is configured and employees are seeded, submit a booking request via the gateway.

```bash
TOKEN=$(./tools/dev-auth.sh employee1)

# Submit a booking request for tomorrow at Prague
TOMORROW=$(date -v+1d +%Y-%m-%d 2>/dev/null || date -d tomorrow +%Y-%m-%d)

curl -s -X POST http://localhost:10000/bookings \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"locationId\": \"Prague\",
    \"date\": \"$TOMORROW\",
    \"reason\": \"onboarding smoke test\"
  }" | python3 -m json.tool

# Verify booking appears in the list
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings | python3 -m json.tool
```

**Expected:** Booking request with status `Pending` and employee-visible reason.

---

## Step 10 — Audit Evidence

**Status:** ✅ Implemented

Verify that admin and booking actions produced audit records.

```bash
TOKEN=$(./tools/dev-auth.sh auditor)
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:10000/audit | python3 -m json.tool
```

**Expected:** Audit records covering tenant setup events and the booking submission from step 9.

---

## Summary: Step Status

| Step | Description | Status | Blocker |
|------|-------------|--------|---------|
| 1 | Create tenant workspace | ✅ Implemented | `POST /tenants` implemented; local demo pre-seeds |
| 2 | Configure identity and role mapping | 🔧 Manual + 🟡 Eval | IdP config UI, per-tenant mapping API |
| 3 | Create first administrator | 🟡 Evaluation-grade | First-admin provisioning API |
| 4 | Parking bootstrap (location, policy, slots) | 🟡 Evaluation-grade | Tenant admin web UI |
| 5 | Tenant object storage | 🟡 Pilot-deferred | OPS008C tenant storage provisioning; readiness now shows `ObjectStorageReadiness=Deferred` |
| 6 | Organization branding assets | 🟡 Pilot-deferred | CUST010 branding asset catalog and client config; readiness now shows `BrandingReadiness=Deferred` |
| 7 | Employee and profile bootstrap | 🟡 Eval (seed) / ✅ API | Web HR import upload (DATA002) |
| 8 | Readiness check | ✅ Implemented | Local demo uses connected HTTP health probes; object-storage and branding are reported as Deferred (non-blocking); deeper tenant-specific evidence is future hardening |
| 9 | First booking smoke | ✅ Implemented | — |
| 10 | Audit evidence | ✅ Implemented | — |

---

## Follow-up Issues

The following gaps must be resolved before this scenario can run without manual or evaluation-grade shortcuts in a real pilot:

- Tenant admin web UI for tenant creation via `POST /tenants` (API implemented, no web form yet)
- Tenant admin web UI for location/slot/policy setup (UX001 track)
- First-admin provisioning API (CUST004)
- Web-based HR import upload (DATA002)
- Full readiness probe implementations (CUST007 track)
- Tenant object storage provisioning, including demo MinIO bucket/prefix and readiness probe (OPS008C)
- Controlled document upload catalog with role checks, metadata, retention category, checksum, and audit records (CUST009)
- Organization branding asset upload and client-config loading (CUST010)
- Tenant lifecycle state machine enforcement (`Draft` → `Configured` → `Seeded` → `Ready`)
