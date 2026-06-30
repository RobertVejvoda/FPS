# Commercialisation Impact Review

This page is a documentation-only guardrail. It does not approve product Billing implementation.

FairSpot should recover cost without weakening the open, inspectable fairness engine that makes the product credible. Commercialisation starts with paid services around adoption, support, and production readiness. Product Billing remains deferred until a real commercial offer has been validated.

## Decision Summary

| Area | Decision |
| --- | --- |
| Product posture | Start small, open-source, parking-first, and pilot-led. |
| Target customer | Small companies, initially below about 150 employees, with scarce parking capacity and visible allocation friction. |
| Core story | Fair allocation, low HR administration, transparent outcomes, client-owned data, and privacy-conscious employee records. |
| Pricing | No public price, tier, discount, or sales promise is approved. Internal willingness-to-pay hypotheses must not appear as commitments. |
| Billing implementation | Deferred until the commercial offer is approved. |
| First paid path | Support, implementation, pilot setup, production readiness, client-specific integration, and enhanced reporting packs. |
| License posture | AGPL open core remains the default. Future dual licensing needs explicit legal and business approval. |
| Open-core line | The open core (runtime + fairness + tenant self-administration) stays public and AGPL. The commercial line is the **platform plane** — the hosted operator product in a future private `fairspot-platform` repo (#660, #633, #642). See the [Open-Core Documentation Boundary](./open-core-boundary). |

## Licensing Decision (2026-06-28)

Robert confirmed the licensing posture; this resolves the dual-licensing open question previously listed below.

- **Stay open — AGPL-3.0.** FairSpot is not going closed. The repository stays AGPL-3.0-or-later (see [Licensing](./licensing)); the open core's credibility depends on being inspectable.
- **Open core** — the fairness/Draw engine, audit/evidence, and the employee- and tenant-facing application. These stay open.
- **Commercial layer = the platform plane** — multi-tenant orchestration, the hosted control plane, billing, usage metering, onboarding/tenant-lifecycle, and advanced resource **modules** (chairs/desks/lockers). Kept structured so it can be proprietary or separately/commercially licensed. The tenant/platform-plane split in epic #633 is the open-core seam; the public/private classification is the [Open-Core Documentation Boundary](./open-core-boundary).
- **Monetize** via hosting + services + modules + a **dual license** (a commercial license for AGPL-averse enterprises).
- **Dual licensing: yes, to be offered** as a future commercial option for enterprises that cannot accept AGPL — but only as an explicit, separate decision. **Actual terms are TBD with legal**; do not publish or imply license terms before then.
- **Keep contributor IP clean** (solo / CLA) to preserve relicensing optionality.
- **Revisit trigger only:** re-open this decision only on a real trigger — a damaging competitor fork, or a concrete deal blocked purely by AGPL.

**Build constraint for the platform epic (#633):** PLAT slices must keep the platform / billing / usage-metering / module layer **structured so it can live in the private `fairspot-platform` / commercial layer** — do not fold it into the AGPL open core by accident. Generic runtime, fairness, audit, and tenant code stays open; platform-plane control, metering, billing, and paid modules stay separable and proprietary-able.

Follow-up (separate, legal): draft the actual dual-license terms before any commercial license is offered.

## Free And Open Core Boundary

The free/open core must be useful enough for a company to evaluate and run a normal tenant. Do not make the fairness story depend on paid unlocks.

| Capability | Free/open core expectation | Why it stays open |
| --- | --- | --- |
| Tenant setup | Standard tenant creation, identity setup, first administrator, policies, slots, and readiness checks. | A company must be able to prove FairSpot can operate before discussing commercial services. |
| Employee workflow | Login, request parking, view bookings, cancel, confirm usage, receive operational notifications, and see safe allocation outcomes. | Employee trust is the product, not an add-on. |
| Fair allocation | Draw, same-day allocation, penalties, reallocation, reason codes, and employee-safe explanations. | Allocation logic must remain inspectable to be trusted. |
| Audit and privacy | Tenant-scoped audit, pseudonymised records, erasure support, and minimal employee facts. | Data ownership and privacy are core objections during evaluation. |
| Basic reporting | Utilization, fairness, outcome, reason-code, and export evidence needed for normal operation. | Sponsors need evidence that FairSpot improves administration and fairness. |
| Client-owned operation | Local, demo, and client-owned production guidance using Dapr and portable observability boundaries. | The strongest trust story is that the customer can run and inspect the system. |

## Commercial Candidates

These are planning candidates, not product promises.

| Candidate | Shape | Notes |
| --- | --- | --- |
| Pilot setup package | Fixed-scope setup for one tenant, one or two parking locations, demo seed, identity mapping, and pilot checklist. | Best first commercial motion because it reduces customer adoption effort without hiding product features. |
| Production readiness review | Deployment, observability, backup/restore, secret handling, security, and operational responsibility review. | Fits the client-owned deployment story. |
| Support subscription | Response targets, release guidance, upgrade help, security advisory handling, and operational assistance. | Should be contract-level, not tied to employee booking behavior. |
| Client-specific integration | IdP, HR profile facts, access control, parking hardware, BI, or workplace calendar integration. | Generic contracts stay open; customer-specific adapters can be paid work. |
| Enhanced reporting pack | Executive summaries, scheduled report packs, custom exports, and client-specific KPIs. | Standard operational reports stay open. |
| Hosted demo or sandbox | FairSpot-operated evaluation environment with synthetic or approved demo data. | Must stay separate from client production ownership and must be cost-controlled. |
| Dual license | Commercial license for customers who cannot accept AGPL obligations. | Requires explicit legal/business approval before being offered. |

## What Not To Monetise

- Do not hide fairness rules, Draw evidence, or audit behavior behind paid features.
- Do not make basic tenant operation, booking, or employee trust screens paid-only.
- Do not make privacy controls or data export/removal evidence paid-only.
- Do not couple cost recovery to individual employee allocation outcomes by default.
- Do not introduce broad employee personal data into commercial records.

## Billing Impact

Billing is not currently a product module. `BILL001` can only start after a follow-up approval answers:

- what is being sold: support, implementation, hosted demo, dual license, or product subscription;
- who pays and who administers the contract;
- which data is required for commercial records and which employee data is explicitly excluded;
- whether invoice handling belongs inside FairSpot or outside FairSpot in accounting tooling;
- which financial-record, tax, privacy, security, and operational obligations apply.

Until then, FairSpot should avoid in-product financial collection workflows and subscription enforcement.

## Data And Trust Impact

The commercial story should reinforce the current customer-integration posture:

- FairSpot should prefer company identity-provider subjects and tenant-scoped pseudonymous records over names, employee IDs, or broad HR profiles.
- Employee booking data is for parking operations, audit, reporting, and fair allocation. It should not become commercial input without a separate approved decision.
- Contract-level commercial contacts, if later needed, should be tenant-scoped and separate from employee booking records.
- Client-owned deployment remains a strong default for companies that treat employee and parking data as sensitive.

## Validation Plan

Validate commercial value before implementing Billing:

| Step | Evidence |
| --- | --- |
| Pilot with one or two small companies | Can the tenant be onboarded, tested, and operated with limited support? |
| Measure admin reduction | Compare email/spreadsheet effort before and after FairSpot. |
| Measure fairness trust | Track complaints, manual overrides, employee questions, and explanation gaps. |
| Measure operational load | Track setup time, support questions, incident/debug effort, and upgrade effort. |
| Validate paid-services fit | Test whether customers value setup, support, production review, and integration help before building product Billing. |

## Open Questions For Later

- What support response targets are commercially realistic?
- Is a hosted demo worth the operational and data-protection burden?
- Which enhanced reports would a sponsor actually pay for?
- **Resolved (2026-06-28):** dual licensing *will* be offered as a future commercial option for AGPL-averse enterprises; actual terms are TBD with legal. See [Licensing Decision (2026-06-28)](#licensing-decision-2026-06-28).
- Does FairSpot need in-product Billing, or is external contract/accounting workflow enough?
