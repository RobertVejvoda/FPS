# Platform Operator Dashboard — Wireframes (PLAT008-UX)

**Status:** Design for review — wireframes only, no implementation.
**Tracks:** Issue #647. Part of epic #633; prepares #646 (PLAT008 build).
**Depends on:** PLAT001 (#634 / PR #645) for the platform roles + auth gate.

> Design contract: this is the FairSpot **operator** console — the product/business
> surface for running the platform across all tenants. It is **not** the tenant app and
> **not** a Grafana replacement. Every section below is annotated with *who sees it*,
> *its data source*, *current availability*, and *action behaviour*. Where a source does
> not exist yet, the screen shows an explicit **Not wired yet** state — never fake status.

---

## 1. Principles

- **Separate surface, platform skin.** Served at `platform.<domain>`, behind the **platform realm/issuer** (PLAT001). FairSpot-operator branding only — never tenant-branded.
- **Read-first.** Mostly reads over platform-plane APIs (Customer lifecycle/readiness/identity, DataHub usage ledger, Audit) + a few platform actions (provision, suspend/archive, reset demo).
- **Links out, doesn't duplicate.** Raw infra detail stays in Grafana/`ops.<domain>`; the dashboard links out.
- **Fail honest.** A source that isn't wired renders **Not wired yet**, with the slice that will provide it.
- **Every mutation is audited** and role-gated.

## 2. Access & roles

The console is reachable **only** with a platform-issuer token. A customer/tenant token (any tenant role, even `admin`) is rejected at the gate — it never reaches the platform plane (enforced by PLAT001's issuer gating).

| Capability | `platform_admin` | `platform_operator` | `platform_auditor` |
|---|---|---|---|
| Landing / health / tenant directory | ✅ | ✅ | ✅ (read) |
| Tenant detail — Overview/Config/Identity/Lifecycle | ✅ | ✅ | ✅ (read) |
| Usage figures | ✅ | ✅ | ✅ (read) |
| **Real $ cost view** | ✅ | ❌ hidden | ❌ hidden |
| Onboarding triage (approve/reject) | ✅ | ✅ | ❌ |
| Provision / suspend / archive tenant | ✅ | ⚠️ provision only | ❌ |
| Reset demo sandbox | ✅ | ✅ | ❌ |
| Audit / evidence views | ✅ | ✅ | ✅ |
| Any mutating action | ✅ | scoped (above) | ❌ read-only |

> The **$ cost** view is platform-admin-internal — the locked rule that tenants never see real cost, extended so operators see *usage* but only `platform_admin` sees *usage→$*.
> Future **MSP/partner** tier (left room for in #633): a scoped console showing only *their* group of tenants — same screens, filtered.

## 3. Information architecture

```mermaid
flowchart LR
  Login[Platform login\n(platform realm)] --> Landing
  Landing[Overview / red-flags] --> Tenants[Tenant directory]
  Landing --> Onboarding[Onboarding queue]
  Landing --> Health[Platform health]
  Tenants --> Detail[Tenant detail\n(tabbed)]
  Detail --> Grafana[(Grafana link-out)]
  Onboarding --> Detail
  Landing -.future.-> Usage[Usage & cost]
  Landing -.future.-> Demo[Demo sandbox]
  Landing -.future.-> Feedback[Feedback]
```

Left-nav order: **Overview · Tenants · Onboarding · Health · Usage* · Demo* · Feedback* · Audit** (`*` = later slices, shown disabled with a tooltip until wired). MVP for #646 = **Overview + Tenants + Tenant detail + Onboarding + the red-flags/health strip**.

---

## 4. Screen — Platform landing (Overview)

```
┌ FairSpot · Platform ─────────────────────────────[ operator: ana ▾ ]┐
│ Overview  Tenants  Onboarding  Health  Usage·  Demo·  Feedback· Audit │
├──────────────────────────────────────────────────────────────────────┤
│  RED FLAGS                                                            │
│  ┌──────────────┬──────────────┬──────────────┬───────────────────┐  │
│  │ ⛔ Vault       │ ⚠ Draws       │ ✅ Boundary    │ ⚠ Backups          │  │
│  │ SEALED (nas-1)│ 1 failed      │ smoke green   │ 1 tenant overdue   │  │
│  └──────────────┴──────────────┴──────────────┴───────────────────┘  │
│  ┌──────────────┐                                                    │
│  │ ⏳ Demo stale  │  (Green Logistics last reset 31h ago)             │
│  └──────────────┘                                                    │
├──────────────────────────────────────────────────────────────────────┤
│  TENANTS              │  ONBOARDING            │  ACTIVITY (7d)        │
│   Ready        8      │   Pending      3       │  Active users  412    │
│   Provisioning 1      │   In review    1       │  Draws run     56     │
│   Suspended    1      │   ───────────          │  Top: Acme (120 u)    │
│   Archived     2      │   [ Open queue → ]     │  [ Usage* ]           │
│  [ Open tenants → ]   │                        │                      │
└──────────────────────────────────────────────────────────────────────┘
```

| Section | Visible to | Source | Availability | Action |
|---|---|---|---|---|
| Red flag: Vault sealed | all | NAS Vault `sys/seal-status` (via the start-stack preflight / a health probe) | **Live** (NAS profile) | Read-only; links to the unseal runbook |
| Red flag: Draw failures | all | DataHub draw history (`drawFailed` projections) | **Live** | Click → filtered tenant list |
| Red flag: Boundary smoke | all | Hosted public-boundary smoke evidence (SEC011) | **Live** (last run); **Not wired yet** for continuous | Read-only |
| Red flag: Backups overdue | all | OPS019 backup evidence checklist | **Not wired yet** (operator-recorded; no API) → shows "manual evidence" | Read-only |
| Red flag: Demo stale | all | Demo sandbox last-reset (PLAT003 #636) | **Not wired yet** → "Demo control pending" | — |
| Tenant state summary | all | Customer lifecycle API | **Live** | Click → directory filtered by state |
| Onboarding counts | all | TenantRequest (PLAT004 #637) | **Not wired yet** → "Onboarding pending" | Click → queue |
| Activity (7d) | all (figures); cost hidden < admin | DataHub usage ledger (PLAT005 #638) | **Not wired yet** → "Usage pending" | Click → Usage |

---

## 5. Screen — Tenant directory

```
┌ Tenants ──────────────────────────────────────[ + Request a tenant ]─┐
│ Filter: [ state ▾ ] [ region ▾ ] [ module ▾ ]   Search [__________]  │
├───────┬──────────┬────────┬────────┬─────────┬────────┬──────────────┤
│ Tenant│ State     │ Region │ Modules│ Usage   │ Health │ Last activity│
├───────┼──────────┼────────┼────────┼─────────┼────────┼──────────────┤
│ Acme  │ ● Ready   │ eu     │ Parking│ 120 u   │ ●●●●○  │ 4 min ago    │
│ Globex│ ● Ready   │ us     │ Parking│  38 u   │ ●●●○○  │ 2 h ago      │
│ G.Log │ ◐ Sandbox │ eu     │ Parking│  —      │ ●●●●●  │ live (demo)  │
│ Initd │ ◑ Provis. │ eu     │ —      │  —      │ ○○○○○  │ —            │
│ OldCo │ ⏸ Suspend │ eu     │ Parking│  0 u    │ ⚠      │ 14 d ago     │
└───────┴──────────┴────────┴────────┴─────────┴────────┴──────────────┘
  Empty: "No tenants match these filters."  Loading: skeleton rows.
  Error: "Couldn't load tenants — retry." (never a blank/fake grid)
```

| Column | Source | Availability |
|---|---|---|
| Tenant / State / Region | Customer lifecycle API | **Live** |
| Modules | module registry (PLAT007 #640) | **Not wired yet** → "Parking" assumed |
| Usage snapshot | DataHub usage ledger (#638) | **Not wired yet** → `—` |
| Health score | composite: readiness + recent activity + error rate + draw success | **Partial** (readiness Live; activity/draw via DataHub) → shows what's available |
| Last activity | DataHub / Audit last event | **Partial** |

`+ Request a tenant` is `platform_admin`/`operator` only and opens the onboarding intake.

---

## 6. Screen — Tenant detail (tabbed)

```
┌ Acme  ● Ready  · eu · Parking ───────────[ Suspend ]·admin [ Archive ]·admin ┐
│ Overview │ Config │ Identity │ Usage │ Lifecycle │ Audit                     │
├──────────────────────────────────────────────────────────────────────────────┤
│ OVERVIEW                                                                      │
│  Health ●●●●○   Readiness: Ready    Created 2026-05-02   Admins: 2            │
│  Active users (7d) 120     Draws (7d) 14 ok / 0 failed                        │
│  Region eu · TZ Europe/Prague · Support contacts: 1                           │
│  Modules: Parking          Cost (30d): $—  ·············· (platform_admin)    │
└──────────────────────────────────────────────────────────────────────────────┘
```

| Tab | Visible to | Source | Availability | Actions |
|---|---|---|---|---|
| **Overview** | all (cost: admin only) | Customer + DataHub | **Partial** (lifecycle/readiness Live; usage/cost Not wired yet → `$—`) | — |
| **Config** | all | Configuration policy/capacity (effective config) | **Live** | edit gated to `platform_admin`/`operator`; audited |
| **Identity / role mapping** | all | Customer identity config + `TenantRoleMapping` | **Live** | edit → `platform_admin`; **audited**; shows trusted-realm-roles + per-tenant mapping |
| **Usage** | all (figures); cost admin only | DataHub usage ledger (#638) | **Not wired yet** → "Usage ledger pending (PLAT005)" | — |
| **Lifecycle history** | all | Customer transitions API | **Live** | transition (suspend/archive) → `platform_admin`; audited |
| **Audit links** | all | Audit service (cross-tenant evidence) | **Live** (links); deep view → `platform_auditor`/`admin` | read-only |

> Each tab header carries an availability badge (**Live** / **Partial** / **Not wired yet**) so reviewers see the build order at a glance.

---

## 7. Screen — Onboarding queue

```
┌ Onboarding ──────────────────────────────────────────────────────────┐
│  REQUESTED (3)     │ APPROVED (1)     │ PROVISIONING (1) │ READY (8)   │
│ ┌────────────────┐ │ ┌──────────────┐ │ ┌──────────────┐ │  …          │
│ │ Northwind      │ │ │ Initech      │ │ │ Initech-2    │ │             │
│ │ northwind.com  │ │ │ approved by  │ │ │ ▓▓▓▓░ realm  │ │             │
│ │ jo@northwind   │ │ │ ana · 1d ago │ │ │ + seed       │ │             │
│ │ "30 sites…"    │ │ │ [ Provision ]│ │ │              │ │             │
│ │ [Approve][Rej] │ │ └──────────────┘ │ └──────────────┘ │             │
│ └────────────────┘ │                  │                  │             │
└──────────────────────────────────────────────────────────────────────┘
  Card = company · domain · contact · message. Drag/began = lifecycle transition.
```

| Element | Visible to | Source | Availability | Action |
|---|---|---|---|---|
| Request cards | `platform_admin`/`operator` (PII-bearing) | TenantRequest store (PLAT004 #637) | **Not wired yet** → "Onboarding intake pending (PLAT004)" | Approve/Reject → lifecycle transition; **audited** |
| Provision step | `platform_admin`/`operator` | Provisioning workflow over Customer lifecycle | **Partial** (lifecycle Live; realm/seed automation later) | Provision → audited |
| Ready column | all | Customer lifecycle | **Live** | open tenant detail |

> Doubles as the **sales funnel + "need for new features" signal board** (the cards capture what prospects ask for).

---

## 8. Mutating controls & required audit evidence

| Control | Role | Audit evidence recorded |
|---|---|---|
| Approve / reject onboarding request | admin/operator | actor, request id, decision, reason |
| Provision tenant | admin/operator | actor, tenant id, provisioning steps, outcome |
| Suspend / archive tenant | admin | actor, tenant id, from→to state, reason |
| Edit identity / role mapping | admin | actor, tenant id, before/after mapping |
| Reset demo sandbox | admin/operator | actor, sandbox tenant id, snapshot id (must be the flagged demo tenant only) |
| Edit config/policy | admin/operator | actor, tenant id, changed fields |

Operator actions are themselves audit events → visible to `platform_auditor`.

## 9. Availability legend (build order)

| Badge | Meaning | Backing slice |
|---|---|---|
| **Live** | source exists today | Customer lifecycle/readiness/identity/config; Audit; SEC011 boundary; NAS Vault status |
| **Partial** | some fields live, some pending | health score, last-activity, provisioning |
| **Not wired yet** | render explicit empty state, no fake status | Usage/cost (PLAT005 #638), Demo control (PLAT003 #636), Onboarding intake (PLAT004 #637), Modules (PLAT007 #640), Feedback (PLAT006 #639) |

## 10. Separation & skin

- Platform skin only; never load tenant branding/theme.
- No tenant-facing copy or simulation footer.
- Deep infra (traces, container metrics, log search) → **link out to Grafana/`ops.<domain>`**, not re-rendered here.
- The console and the tenant app share a component library but are different apps on different hostnames behind different realms.

---

## Open questions for review

1. Hostname: `platform.<domain>` vs a path under `ops.<domain>` (behind Cloudflare Access)?
2. Should `platform_operator` see usage *figures* or only health (the table assumes figures, cost hidden)?
3. Demo reset confirmation: typed-tenant-name confirm (since it's destructive), even though it's flagged sandbox-only?
4. MVP cut for #646 — confirm: Overview + Tenants + Tenant detail + Onboarding + red-flags/health, everything else disabled-with-tooltip.
