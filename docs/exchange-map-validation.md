# Exchange Map Validation

This note validates the exported exchange-map asset against current docs and code.

Design asset checked: [docs/images/fairspot-exchange-map.png](./images/fairspot-exchange-map.png)

Raw source: [docs/fairspot.drawio](./fairspot.drawio), diagram `exchange-map`

## Summary

The exchange map is the target business-domain picture. This validation checks how much of that picture is already explained and implemented, not whether every shown domain must be delivered now.

The strongest implemented path is:

`Configuration/Profile -> Booking -> booking-events -> Notification/Audit/Reporting`

The former app-readiness gap was Customer Service persistence: tenant registry, identity configuration, first admins, and parking bootstrap data needed to survive service restart. That has moved to Dapr-backed Customer repositories; remaining validation is hosted restore/smoke evidence and keeping tenant-scoped storage contracts honest. Billing is not a customer-first priority. Feedback is lower-risk and reasonable for testing/demo support because it can capture evaluator issues without changing the allocation core. Reporting currently consumes Booking events directly, not Audit events, while DataHub is the durable read-model direction.

## Exchange Validation

| Exchange-map element | Diagram says | Docs say | Code status | Validation |
| --- | --- | --- | --- | --- |
| Configuration -> Booking | Configuration provides available slots. | Booking may consume tenant/location policy, time slots, capacity pools, slot capability rules, Draw settings, same-day policy, cancellation/no-show policy. | `Configuration` service exists. Booking still uses `DefaultTenantPolicyService` and `ConfiguredAvailableSlotService` stubs rather than the Configuration API as the authoritative source. | Partial. Domain exists, but the runtime exchange is not fully implemented. Diagram label is too narrow because the contract is broader than available slots. |
| Profile -> Booking | Profile provides user's subject capabilities. | Booking consumes a Profile snapshot with eligibility, vehicle, company-car, accessibility, reserved-space, and snapshot version fields. | Implemented through `HttpProfileSnapshotService` calling `profile/snapshot`; Profile snapshot tests cover `isDefault` and active vehicles. | Mostly implemented. The visible label should say eligibility and vehicle/profile snapshot rather than only subject capabilities. |
| Booking Requestor -> Booking | Request. | Booking owns request lifecycle and uses authenticated context for employee scoping. | Implemented through Booking API `POST /bookings`, `GET /bookings`, cancellation, confirmation, and My Spots/web/mobile flows. | Implemented for current employee scope. |
| Booking Adjuster -> Booking | Adjust. | Manual correction exists as `B010`; HR operations cancellation is tracked separately. | Manual correction endpoint exists; HR operations has recently been added for request queues/cancellation/draw controls. | Implemented for current admin/HR support scope, but role wording should be updated to HR/facilities/admin language. |
| Booking Processor/System -> Booking | Slot allocation. | Scheduled/manual Draw, same-day allocation, expiry, no-show, retry must be idempotent and auditable. | Draw trigger and lifecycle/status APIs exist. Automatic scheduler/runtime loop is not clearly represented as a production scheduler exchange. | Partial. Manual/system-triggered processing exists; production scheduling should be clarified separately. |
| Booking -> Notification | Raise; user receives notifications. | Booking publishes notification events after authoritative state changes; Notification failure must not roll back Booking state. | Booking publishes `booking-events`; Notification subscribes via Dapr topic and stores in-app/email notifications with deduplication. | Implemented. |
| Booking -> Audit | Send business logs. | Booking publishes audit events for meaningful state transitions and allocation decisions. Business-facing screens must use Audit records, not raw logs. | Audit subscribes to `booking-events`, pseudonymises actors, stores append-only records, and supports query/erasure/retention/integrity/export. | Implemented, but diagram wording is stale. Use "audit events" or "business events", not "business logs". |
| Booking -> Reporting | Not shown directly in the visible diagram. | Reporting consumes Booking read models or events; Reporting must not drive Booking state. | Reporting subscribes directly to `booking-events` and materializes operational metrics/fairness read models. | Diagram gap. Add Booking -> Reporting or change the current Audit -> Reporting line. |
| Audit -> Reporting | Audit events. | Current docs do not make Audit the source of Reporting metrics; Audit is evidence, Reporting is read models/aggregates from Booking outcomes. | No Audit-to-Reporting event path found. Reporting consumes Booking events. | Stale/misleading. Replace with Booking -> Reporting unless a future audit-derived reporting path is intentionally designed. |
| Customer -> Reporting | Request reports. | Customer/readiness checks may verify Reporting readiness; reporting views are separate product surfaces. | Customer service has readiness probes including Reporting, but no report-request workflow from Customer to Reporting. | Partial/stale. Clarify as tenant readiness/checking if kept. |
| Customer -> Audit | Send invoicing events / customer events implied. | Customer lifecycle/configuration changes should be auditable where implemented; Billing is deferred. | Customer service exists, but a general Customer-to-Audit event stream is not clearly implemented in the same way Booking events are. | Partial/gap. Needs either implementation evidence or diagram simplification. |
| Customer Service domain | Customer manages/subscribes tenant and is referenced by reporting/audit/billing flows. | Customer onboarding, tenant lifecycle, identity setup, parking bootstrap, employee/profile bootstrap, and readiness checks are documented. Tenant storage contract says Customer owns tenant registry, onboarding state, and identity configuration. | `FPS.Customer` implements tenant lifecycle, identity config, first admins, parking bootstrap, readiness probes, tenant requests, and Dapr-backed repositories for the runtime path. In-memory repositories remain for tests. | Implemented for the current baseline. Hosted restore/smoke evidence and operator runbooks remain the customer-ready proof path. |
| Billing domain | Billing generates invoices, sends payment/invoicing events, sends notifications. | Billing is explicitly deferred and documentation-only until a commercial offer is approved. | No `code/server/Billing` service exists. | Correct as target design, but not priority for making the app work. Keep out of current app delivery unless commercial scope changes. |
| Feedback domain | User sends feedback. | Feedback docs currently say deferred, but a lightweight feedback path is reasonable for testing/demo support. | No `code/server/Feedback` service exists. | Correct as target design. Implementation gap is acceptable, but a small test-feedback slice could be useful before customer evaluation. |
| Identity domain | Administrator manages identity; identity is used by the system. | ID001/ID002 define authenticated user context, tenant claim, role mapping, SSO-first setup, local fallback. | `Identity` service and shared current-user/role-mapping code exist; Customer owns tenant identity setup. | Implemented for current baseline. |

## Documentation Gaps

- [docs/business-layer/booking-context-contract.md](./business-layer/booking-context-contract.md) is the best current written contract for Booking exchanges and should remain the source for implementation boundaries.
- [docs/business-layer.md](./business-layer.md) embeds the exchange map but does not distinguish target domains from current delivery priorities.
- [docs/business-layer/booking.md](./business-layer/booking.md) still contains older generic sections such as AI Service, Administration Service, and Communication Service that do not match the current service boundaries as clearly as the context contract does.
- Customer Service durable persistence is implemented for the runtime path; keep hosted restore/smoke evidence current.
- Billing docs correctly say deferred and should stay that way for now.
- Feedback docs may be too strongly deferred if the near-term app needs a simple testing/evaluator feedback path.

## Recommended Diagram Updates

When the exchange map is updated, make these changes first:

1. Keep Billing and Feedback in the target exchange map, but visually distinguish current-baseline domains from future/lower-priority domains if the diagram is used for delivery status.
2. Mark Customer Service as implemented baseline, with hosted evidence still required before real customer data.
3. Rename Booking -> Audit from "send business logs" to "publish audit/business events".
4. Add Booking -> Reporting with "booking outcome events/read models".
5. Remove or relabel Audit -> Reporting unless a real Audit-to-Reporting flow is implemented.
6. Broaden Configuration -> Booking from "available slots" to "policy, capacity, slot capabilities".
7. Broaden Profile -> Booking to "eligibility and vehicle/profile snapshot".
8. Rename actor roles to current product language: Employee, HR/facilities, Tenant administrator, Auditor, Reporting viewer.

## Implementation Follow-Ups

- Keep Customer durable storage evidence linked from readiness docs and release validation.
- Create a Configuration-to-Booking integration slice if Booking should stop using policy/capacity stubs for customer pilot traffic.
- Decide whether Customer service changes must publish auditable events through the same reliable event pattern as Booking.
- Keep Billing out of customer-facing "implemented" materials until a commercial slice is approved.
- Consider a small Feedback slice for testing/customer-evaluation support: authenticated feedback submission, tenant/user context from auth, basic status/category, and admin/support viewing. Avoid public anonymous intake and avoid building a broad support desk.
