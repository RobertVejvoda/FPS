# Business Processes

This page is the target business process catalog. Detailed executable rules still live in the legacy business-layer contracts until the information systems slice migrates the implementation contracts.

## Process Catalog

| Process | Trigger | Main Outcome | Status | Source Evidence |
| --- | --- | --- | --- | --- |
| Tenant setup and readiness | New company tenant is prepared. | Tenant is configured, identity is mapped, initial admin can authenticate, policy/location/capacity/profile facts exist, readiness evidence is recorded. | Partial | [Tenant Onboarding](/business-layer/tenant-onboarding), [Business Process Flows](/business-layer/business-process-flows) |
| Future parking request | Employee requests a future parking time slot. | Valid request becomes `Pending` for scheduled Draw, or `Rejected` with employee-safe reason. | Partial | [Booking](/business-layer/booking), [Booking Request Lifecycle](/business-layer/booking-request-lifecycle) |
| Scheduled Draw and allocation | Request window closes or authorized HR/facility user triggers Draw. | Requests become `Allocated`, `Rejected`, or remain `Pending` waitlist according to policy and capacity. | Partial | [Allocation Rules](/business-layer/allocation-rules), [Draw Scheduling](/production/draw-scheduling-and-workflow) |
| Same-day parking request | Employee requests parking for the current day after scheduled Draw path is no longer applicable. | Immediate allocation or rejection based on policy and matching live capacity; same-day waitlist is future policy scope. | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Allocation Rules](/business-layer/allocation-rules) |
| Cancellation, reallocation, usage, and no-show | Employee, HR, system, or confirmation process changes an active request. | Capacity is released/reallocated where possible; usage, no-show, expiry, penalties, notifications, and audit evidence are recorded. | Partial | [Booking Request Lifecycle](/business-layer/booking-request-lifecycle), [Allocation Rules](/business-layer/allocation-rules) |
| HR and administrator operations | Privileged role needs to manage operations or tenant setup. | Role-specific workspace supports queues, next Draw visibility, controlled Draw, cancellation with reason, tenant setup, policy, and readiness. | Placeholder | [Roles](/business-layer/roles), [Role Intent Roadmap](/business-layer/role-intent-roadmap), [My Spots UX](/business-layer/my-spots-ux) |
| Reporting and audit evidence | Booking events, privileged actions, or review request occurs. | Audit evidence and DataHub-backed operational projections support safe reporting and review. | Placeholder | [Audit](/business-layer/audit), [Reporting](/business-layer/reporting), [DataHub](/application-layer/datahub) |
| Pilot feedback | Authenticated pilot user submits feedback. | Tenant-scoped feedback is captured, reviewed, optionally answered, and audited where sensitive. | Deferred | [Feedback](/business-layer/feedback) |
| Billing and payment | Commercial model is approved in future. | Tenant-level commercial records may be managed separately from employee booking details. | Deferred | [Billing](/business-layer/billing), [Commercialisation](/strategy-layer/commercialisation) |

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
3. HR/facilities can run controlled on-demand Draw actions where allowed.
4. HR/facilities can cancel any tenant-scoped request or allocation with a required reason and employee notification.
5. Tenant administrator manages tenant configuration, locations, policies, roles, identity mapping, readiness, and setup data.
6. System administrator sees platform/operator views that are not employee or HR defaults.
7. Sensitive actions publish events, notify affected users, and write audit records.

Visible gap: role-specific HR, tenant administrator, and system administrator default screens must be implemented and validated.

## Business Process Diagram Placeholders

- Tenant setup and readiness process.
- Employee future request process.
- Scheduled Draw process.
- Same-day request process.
- Cancellation and reallocation process.
- HR/admin operations process.
- Reporting and audit evidence process.
