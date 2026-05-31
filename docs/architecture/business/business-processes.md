# Business Processes

This page is the target business process catalog. Detailed executable rules still live in the legacy business-layer contracts until the information systems slice migrates the implementation contracts.

The process summaries below are authoritative for business intent. When a detailed implementation rule is needed, use the referenced legacy contract until that rule is migrated into the appropriate Information Systems contract artifact.

## Process Catalog

| Process | Trigger | Main Outcome | Status | Source Evidence |
| --- | --- | --- | --- | --- |
| Tenant setup and readiness | New company tenant is prepared. | Tenant is configured, identity is mapped, initial admin can authenticate, policy/location/capacity/profile facts exist, readiness evidence is recorded. | Partial | [Tenant Onboarding](/business-layer/tenant-onboarding), [Business Process Flows](/business-layer/business-process-flows) |
| Future parking request | Employee requests a future parking time slot. | Valid request becomes `Pending` for scheduled Draw, or `Rejected` with employee-safe reason. | Partial | [Booking](/business-layer/booking), [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Scheduled Draw and allocation | Request window closes or authorized HR/facility user triggers Draw. | Requests become `Allocated`, `Rejected`, or remain `Pending` waitlist according to policy and capacity. | Partial | [Allocation Rules](/business-layer/allocation-rules), [Draw Scheduling](/production/draw-scheduling-and-workflow) |
| Same-day parking request | Employee requests parking for the current day after scheduled Draw path is no longer applicable. | Immediate allocation or rejection based on policy and matching live capacity; same-day waitlist is future policy scope. | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Allocation Rules](/business-layer/allocation-rules) |
| Cancellation, reallocation, usage, and no-show | Employee, HR, system, or confirmation process changes an active request. | Capacity is released/reallocated where possible; usage, no-show, expiry, penalties, notifications, and audit evidence are recorded. | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Allocation Rules](/business-layer/allocation-rules) |
| HR and administrator operations | Privileged role needs to manage operations or tenant setup. | Role-specific workspace supports queues, next Draw visibility, controlled Draw, cancellation with reason, tenant setup, policy, and readiness. | Placeholder | [Roles](/business-layer/roles), [Role Intent Roadmap](/business-layer/role-intent-roadmap), [My Spots UX](/business-layer/my-spots-ux) |
| Resource map and capacity publication | Facilities or administrator changes physical capacity, zones, capabilities, or closures. | Resource map changes are validated, published, audited, and reflected in policy-compatible allocation and insight views. | Placeholder | [Business Requirements](/business-layer/requirements), [Personas](/business-layer/personas), [Role Intent Roadmap](/business-layer/role-intent-roadmap) |
| Reporting and audit evidence | Booking events, privileged actions, or review request occurs. | Audit evidence and DataHub-backed operational projections support safe reporting and review. | Placeholder | [Audit](/business-layer/audit), [Reporting](/business-layer/reporting), [DataHub](/application-layer/datahub) |
| Pilot feedback | Authenticated pilot user submits feedback. | Tenant-scoped feedback is captured, reviewed, optionally answered, and audited where sensitive. | Deferred | [Feedback](/business-layer/feedback) |
| Billing and payment | Commercial model is approved in future. | Tenant-level commercial records may be managed separately from employee booking details. | Deferred | [Billing](/business-layer/billing), [Commercialisation](/strategy-layer/commercialisation) |

## Process Classification

| Classification | Processes | Architecture Direction |
| --- | --- | --- |
| Customer-ready P0 | Tenant setup and readiness; future parking request; scheduled Draw and allocation; same-day parking request; cancellation/reallocation/usage/no-show; HR and administrator operations; resource map and capacity publication. | Keep visible in target state and transition gaps until implemented and validated. |
| Customer-ready P1 | Reporting and audit evidence. | DataHub projections and Audit evidence must be durable before customer claims are strong. |
| Pilot support P2 | Pilot feedback. | Optional narrow flow for customer evaluation feedback. |
| Deferred | Billing and payment. | Do not include in customer-ready acceptance gates. |

## Core Process Summaries

### Tenant Setup And Readiness

1. Operator or tenant administrator creates the tenant workspace.
2. Customer records tenant display name, slug, lifecycle state, region, timezone, and support contacts.
3. Identity mapping is configured for trusted issuer, audience, stable subject claim, tenant mapping, and role/group mapping.
4. First tenant administrator is created or mapped.
5. Configuration receives parking defaults, location, time slots, capacity/resource data, and capability rules.
6. Profile receives or resolves minimal employee facts needed for pilot users.
7. Readiness check verifies tenant state, identity, administrator access, policy, location/capacity, profile facts, Booking smoke path, Notification, Audit, DataHub/reporting, and durable tenant storage.
8. Tenant moves to `Ready` only after readiness evidence passes and customer accepts launch.

Visible gap: durable Customer/tenant storage is required for customer-ready deployment.

### Future Parking Request

1. Employee selects location, date, time slot, and vehicle/request attributes.
2. Booking receives the request through authenticated tenant/user context. Tenant and user identity must not come from the request body.
3. Booking resolves tenant policy, slot/capacity compatibility, employee eligibility, and vehicle/profile snapshot.
4. Booking validates duplicate request, request cap, cut-off time, eligibility, vehicle requirements, and slot capability compatibility.
5. Valid future requests become `Pending`; invalid requests become `Rejected` with stable employee-safe reason codes.
6. Notification, Audit, and DataHub/read-model events are produced after authoritative state changes.

### Scheduled Draw And Allocation

1. Scheduler or authorized HR/facility user starts Draw for tenant, location, parking date, and time slot.
2. Booking locks the Draw key idempotently so the same Draw cannot allocate twice.
3. Booking loads eligible `Pending` requests and resolves policy, capacity, vehicle capability, reserved-space, company-car, accessibility, and metric snapshots.
4. Tier 1 company-car or reserved obligations are allocated first.
5. Tier 1 overflow is rejected because it indicates tenant configuration drift, not normal lottery loss.
6. Tier 2 weighted fairness allocates remaining capacity using recent allocation count and active penalties.
7. Non-winning eligible Tier 2 requests remain `Pending` by default until cancellation reallocation, expiry, or tenant policy says otherwise.
8. Draw attempt records algorithm version, seed, ordered candidate sequence, decisions, safe reason codes, and audit evidence.

### Same-Day Parking Request

1. Employee submits a request for the current day.
2. Booking checks same-day policy, active time window, duplicate request, eligibility, vehicle compatibility, and matching available capacity.
3. If matching capacity is available and policy permits, Booking allocates immediately.
4. If no matching capacity exists, v1 rejects the request unless a later tenant policy explicitly introduces same-day waitlist.
5. Same-day successful allocations count toward future fairness metrics.

### Cancellation, Reallocation, Usage, And No-Show

1. Requestor or authorized HR/facility role cancels a request.
2. `Pending` cancellation becomes `Cancelled` without late penalty by default.
3. `Allocated` cancellation becomes `Cancelled`, releases the slot, applies late-cancellation penalty where enabled, and starts reallocation.
4. Reallocation uses original Draw ordering when available and skips candidates that are no longer eligible or cannot match the released slot.
5. If no eligible pending request exists, the slot remains available for same-day allocation or manual use under tenant policy.
6. Usage confirmation changes `Allocated` to `Used`.
7. No-show evaluation changes `Allocated` to `NoShow` only when a valid confirmation source and tenant policy support it.
8. Expiry closes no-longer-actionable requests without creating a penalty by default.

### HR And Administrator Operations

1. Privileged user opens a role-appropriate dashboard.
2. HR/facilities can see request queues, allocation outcomes, pending waitlist, cancellation/reallocation state, and next Draw time.
3. HR/facilities can search by safe business reference or permitted employee display value and open request lifecycle evidence without exposing hidden Draw internals.
4. HR/facilities can run controlled on-demand Draw actions where allowed.
5. HR/facilities can cancel any tenant-scoped request or allocation with a required reason and employee notification.
6. Tenant administrator manages tenant configuration, locations, policies, roles, identity mapping, readiness, and setup data.
7. Tenant administrator can preview readiness and policy/capacity impact before publishing customer-visible changes where projections support it.
8. System administrator sees platform/operator views that are not employee or HR defaults.
9. Sensitive actions publish events, notify affected users, and write audit records.

Visible gap: role-specific HR, tenant administrator, and system administrator default screens must be implemented and validated.

### Resource Map And Capacity Publication

1. Facilities coordinator or authorized tenant administrator opens a resource-map workspace.
2. User maintains locations, zones, spaces, capacity pools, EV/accessibility/company-car/reserved capabilities, and temporary closures.
3. FairSpot validates that the map is tenant-scoped, internally consistent, policy-compatible, and does not create impossible capacity promises.
4. User previews visible impact where possible: reduced capacity, shortage risk, unused pools, capability mismatch, and affected time slots.
5. Publication records who changed the map, what changed, effective time, validation result, and reason when required.
6. Booking uses only the published compatible capacity for allocation; drafts or failed publications must not affect Draw decisions.
7. DataHub/read-model projections expose utilization and shortage trends by location, zone, capability, and time period.

Visible gap: resource-map workspace, publication workflow, and policy-impact preview need implementation and customer validation.

### Reporting And Audit Evidence

1. Booking, Customer, Configuration, Profile, Notification, and privileged UI actions produce business events or audit records where relevant.
2. Audit keeps append-only business evidence for setup, policy changes, request decisions, Draw attempts, cancellations, manual actions, and sensitive access.
3. DataHub consumes approved events into tenant-scoped projections for demand, allocation, rejection, cancellation, no-show, fairness, utilization, capacity pressure, readiness, and management summaries.
4. Reporting, if retained, exposes report catalog/configuration or presentation over approved DataHub views.
5. HR/facilities, tenant administrators, auditors, and sponsors see role-appropriate summaries without raw technical logs or unrelated employee data.
6. Executive sponsors see aggregated business-value evidence rather than operational detail: HR effort avoided, unmet demand, allocation rate, fairness trend, utilization, no-show/cancellation trend, and capacity pressure.

Visible gap: DataHub durable projections are required before customer-facing operational reporting should be treated as reliable.

### Pilot Feedback

1. Authenticated pilot user or evaluator submits feedback from web/mobile or a customer-facing support path.
2. Feedback captures tenant/user context from authentication, category, message, optional page/context, and status.
3. The UI warns users not to submit secrets or unrelated personal data.
4. Authorized support/product users review feedback and convert actionable items into GitHub issues or customer follow-ups.
5. Sensitive or policy-relevant feedback receives audit evidence where required.

Visible gap: Robert must decide whether this is needed before the first customer demo or remains deferred.

### Billing And Payment

Billing and payment are not part of the customer-ready business process baseline. Future commercial records, support/subscription metadata, or external invoice references must be tenant-scoped and auditable, but employee booking data must not become commercial input unless a later approved decision changes that boundary.

## Business Rule Contract Migration

| Legacy Contract | Business Architecture Summary | Later Target Location |
| --- | --- | --- |
| [Allocation Rules](/business-layer/allocation-rules) | Draw tiers, weighted fairness, slot matching, resource maps, zones, same-day allocation, cancellation reallocation, penalties, audit, idempotency. | Information Systems API/event/domain contracts and Booking implementation docs. |
| [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) | Statuses, transitions, cancellation, usage confirmation, no-show, expiry, employee-visible outcomes. | API contracts, event contracts, mobile/web UX contracts. |
| [Booking Reason Codes](/business-layer/booking-reason-codes) | Stable reason codes and employee-safe text rules. | API contracts, notification templates, DataHub/report views. |
| [Parking Policy Configuration](/business-layer/parking-policy-configuration) | Tenant/location policy, draw schedule, caps, penalties, resource map, zone, reserved-space, company-car, usage/no-show. | Configuration service contracts and data architecture. |

## Business Process Diagram Placeholders

- Tenant setup and readiness process.
- Employee future request process.
- Scheduled Draw process.
- Same-day request process.
- Cancellation and reallocation process.
- HR/admin operations process.
- Resource map and capacity publication process.
- Reporting and audit evidence process.
- Robert TODO: decide which process diagrams must be authored first for external review.
