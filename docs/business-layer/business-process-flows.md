# Business Process Flows

This document defines the business process flows that sit behind the exchange map. The exchange map shows which business domains exchange information; this page explains the exact operational sequence, business actors, exchanged objects, outcomes, and known readiness gaps.

Use this page as source material for future ArchiMate business process diagrams. The diagrams should show process ownership and exchanges, but this text remains the detailed contract.

## Scope And Priority

FairSpot's customer-first business scope is parking allocation for a tenant:

1. onboard a tenant;
2. configure identity, roles, policy, locations, and parking capacity;
3. let employees request parking;
4. allocate scarce capacity fairly;
5. notify employees and HR;
6. support cancellations, reallocation, usage confirmation, and no-shows;
7. report on outcomes and preserve audit evidence.

Billing and Payment Gateway remain target architecture elements only. They are not part of the customer-first business process baseline.

Feedback is not part of the core allocation process, but a small authenticated feedback flow is reasonable for pilots and customer evaluation.

## Exchange Map Relationship

| Exchange map domain | Business process role | Current priority |
| --- | --- | --- |
| Customer | Tenant workspace, onboarding, readiness, customer administration. | P0 readiness gap because durable Customer storage is required. |
| Identity | Tenant authentication, role mapping, user context. | Baseline. |
| Configuration | Parking policy, location, slot/capacity, time-slot configuration. | Baseline, with runtime integration gaps. |
| Profile | Employee eligibility, vehicle, company-car, accessibility, reserved-space facts. | Baseline. |
| Booking | Request lifecycle, Draw, allocation, cancellation, reallocation, usage outcome. | Core product baseline. |
| Notification | Employee and operational notifications after authoritative business events. | Baseline for in-app/server events; channels are staged. |
| Audit | Append-only evidence for tenant setup, policy changes, booking decisions, manual actions, and sensitive access. | Baseline. |
| Reporting | Tenant-scoped operational reporting and exports from booking outcomes. | Baseline capability with durable relational storage gap. |
| Feedback | Authenticated evaluator/customer feedback. | Candidate near-term pilot support. |
| Billing | Commercial account, invoice, payment flows. | Future/deferred. |

## Process 1: Tenant Setup And Readiness

### Purpose

Create a usable tenant before employees can book parking.

### Actors

- FairSpot operator or tenant administrator.
- Customer sponsor.
- Identity administrator.
- HR or facilities administrator.

### Preconditions

- Customer has agreed to run FairSpot for a tenant.
- Required support contacts, timezone, and expected identity approach are known.
- Billing is not required for customer-first setup.

### Detailed Flow

1. Operator creates the tenant workspace in Customer.
2. Customer records tenant display name, slug, lifecycle state, region, timezone, and support contacts.
3. Customer prepares tenant-scoped provisioning metadata for service storage.
4. Identity configuration is added: trusted issuer, audience, stable subject claim, tenant mapping, and role/group mapping.
5. First tenant administrator is created or mapped.
6. Configuration receives tenant parking defaults, at least one location, time slots, slot or capacity pool data, and relevant capability rules.
7. Profile receives or resolves the minimal employee facts needed for pilot users: active status, role, location, vehicle, company-car, reserved-space, and accessibility facts where policy requires them.
8. Customer readiness check verifies tenant state, identity, administrator access, policy, location/capacity, profile facts, Booking smoke path, Notification, Audit, Reporting, and tenant object storage where enabled.
9. Customer moves tenant lifecycle from `Draft` or `Configured` to `Ready` only after readiness evidence passes and the customer accepts launch.
10. Audit records setup changes, readiness checks, privileged role changes, and lifecycle transitions.

### Outputs

- Tenant workspace.
- Identity and role mapping.
- First administrator.
- Parking policy and location/capacity setup.
- Pilot employee/profile facts.
- Readiness result.
- Audit evidence.

### Exceptions

- If identity validation fails, tenant remains not ready.
- If no administrator can authenticate, tenant remains not ready.
- If no valid policy/location/capacity exists, employee booking stays disabled.
- If durable Customer storage is not available, tenant setup is not customer-ready because setup state cannot be trusted across restart.

### Current Status

Partial. The Customer service boundary exists, but durable Customer storage is a P0 gap tracked as `DATA011`.

## Process 2: Future Parking Request

### Purpose

Accept an employee request for a future time slot and place it into the allocation queue.

### Actors

- Employee.
- Booking system.
- Profile.
- Configuration.
- Notification.
- Audit.

### Preconditions

- Tenant is `Ready`.
- Employee is authenticated and tenant-scoped.
- Employee profile and vehicle facts required by policy are available.
- Requested location/date/time slot is open for requests.

### Detailed Flow

1. Employee opens Web App or Mobile App and chooses location, date, time slot, and vehicle or request attributes.
2. Client submits request to Booking through the API Gateway using authenticated context. Tenant and user identity must not come from the request body.
3. Booking resolves tenant policy and slot/capacity compatibility from Configuration or configured policy service.
4. Booking resolves employee eligibility and vehicle/profile snapshot from Profile.
5. Booking validates duplicate request, request cap, cut-off time, employee eligibility, vehicle requirements, and slot capability compatibility.
6. If validation fails, Booking rejects the request with a stable reason code.
7. If validation passes, Booking stores the request as `Pending`.
8. Booking publishes or records the business event required for Notification, Audit, and Reporting.
9. Notification informs the employee that the request is pending or rejected.
10. Audit records the request decision and validation reason.
11. Reporting projection receives the booking outcome event and updates tenant-scoped operational metrics.

### Outputs

- `Pending` request for the scheduled Draw, or `Rejected` request with reason code.
- Employee-visible notification.
- Audit record.
- Reporting projection event.

### Exceptions

- Duplicate overlapping request: reject with duplicate reason.
- Late request after scheduled Draw cut-off: reject with late reason and guide employee to same-day path if policy allows it.
- Missing required vehicle/profile facts: reject or block with a clear employee-safe reason.
- Unavailable matching slot type: reject with no matching capacity reason.

### Current Status

Mostly implemented for current employee booking flows. Configuration-to-Booking runtime integration remains partial where Booking still uses default/stub policy services.

## Process 3: Scheduled Draw And Allocation

### Purpose

Allocate scarce future parking capacity fairly after the request window closes.

### Actors

- Booking processor or scheduler.
- HR/facilities administrator for manual trigger or oversight.
- Booking.
- Configuration.
- Profile.
- Notification.
- Audit.
- Reporting.

### Preconditions

- Draw key is known: tenant, location, parking date, and time slot.
- Request window is closed.
- Matching `Pending` requests exist.
- Policy, capacity, and profile snapshots are available.

### Detailed Flow

1. Scheduler or authorized HR/facilities user starts the Draw for a specific Draw key.
2. Booking locks the Draw key idempotently so the same Draw cannot allocate twice.
3. Booking loads eligible `Pending` requests for the Draw key.
4. Booking resolves policy, slot/capacity, vehicle capability rules, reserved-space rules, company-car rules, and accessibility constraints.
5. Booking separates mandatory policy allocations from fairness allocations.
6. Booking allocates Tier 1 capacity such as matching company-car or reserved-space obligations.
7. If Tier 1 demand exceeds matching capacity, overflow requests are rejected with the configured reason.
8. Booking computes fairness ordering for remaining eligible requests using the configured allocation rules, recent allocation history, and active penalties.
9. Booking assigns available matching slots to winners.
10. Winning requests move from `Pending` to `Allocated`.
11. Eligible non-winning requests remain `Pending` by default until cancellation reallocation, same-day policy handling, or expiry.
12. Booking records the Draw attempt, algorithm version, seed, ordered candidate sequence, allocation decisions, and safe reason codes.
13. Booking publishes business events.
14. Notification informs allocated and rejected employees.
15. Audit stores business evidence for the Draw and allocation decisions without exposing hidden lottery internals to employees.
16. Reporting updates demand, allocation, rejection, fairness, and utilization projections.

### Outputs

- Allocated reservations.
- Rejected overflow or ineligible requests.
- Pending waitlist requests.
- Draw evidence.
- Notifications.
- Audit records.
- Reporting projections.

### Exceptions

- Draw already completed: return existing result idempotently.
- Capacity changes during Draw: use locked/versioned capacity snapshot or fail safely with auditable reason.
- Missing/corrupt original ordering during later reallocation: use deterministic fallback and audit why fallback was required.
- Notification failure: do not roll back authoritative Booking state.

### Current Status

Implemented enough for manual/admin Draw and lifecycle behavior. Production scheduler clarity and Configuration runtime authority should remain visible follow-ups.

## Process 4: Same-Day Parking Request

### Purpose

Handle requests after the scheduled Draw has already run.

### Actors

- Employee.
- Booking.
- Configuration.
- Profile.
- Notification.
- Audit.
- Reporting.

### Preconditions

- Tenant policy allows same-day requests.
- Requested time slot has not ended.
- Employee is authenticated and eligible.

### Detailed Flow

1. Employee submits a same-day request from Web App or Mobile App.
2. Booking verifies same-day policy, time window, duplicate request, employee eligibility, vehicle compatibility, and available matching capacity.
3. If a matching slot is available and policy permits immediate assignment, Booking allocates the slot immediately.
4. If no matching capacity is available, Booking rejects the request or keeps it `Pending` only if tenant same-day waitlist policy explicitly allows it.
5. Booking publishes business events for the outcome.
6. Notification informs the employee.
7. Audit records the same-day decision.
8. Reporting updates same-day request and outcome projections.

### Outputs

- `Allocated`, `Pending`, or `Rejected` request.
- Employee notification.
- Audit and reporting evidence.

### Exceptions

- Same-day disabled: reject with policy reason.
- Slot has already started or ended beyond policy window: reject with late reason.
- Capacity exists but does not match vehicle/accessibility/company-car constraints: reject with no matching capacity reason.

### Current Status

Part of the current booking direction. Exact same-day waitlist behavior should remain tenant-policy controlled and employee-safe.

## Process 5: Cancellation, Reallocation, Usage, And No-Show

### Purpose

Keep capacity useful after changes and ensure outcomes feed future fairness.

### Actors

- Employee.
- HR/facilities administrator.
- Booking.
- Notification.
- Audit.
- Reporting.

### Preconditions

- A request exists in `Pending` or `Allocated`.
- Actor is the requestor or an authorized HR/facilities/admin role.

### Detailed Flow

1. Employee or authorized role cancels a request.
2. If request is `Pending`, Booking changes it to `Cancelled`, applies no late penalty by default, and publishes the cancellation event.
3. If request is `Allocated`, Booking changes it to `Cancelled`, releases the slot, applies late-cancellation penalty according to policy, and starts reallocation.
4. Reallocation uses original Draw ordering when available.
5. Booking skips candidates that are no longer `Pending`, no longer eligible, or do not match the released slot.
6. First eligible candidate receives the released slot and moves to `Allocated`.
7. If no eligible candidate exists, the slot remains available for same-day allocation or manual operational use under policy.
8. Employee may confirm usage before the confirmation window closes.
9. Valid usage confirmation changes `Allocated` to `Used`.
10. If confirmation is missing and no-show policy is enabled, no-show evaluation changes `Allocated` to `NoShow` and applies penalty.
11. If no-show policy is disabled, the allocation may become `Expired` when no longer actionable.
12. Notifications are sent to affected employees.
13. Audit records cancellation, penalty, reallocation, usage, no-show, or expiry.
14. Reporting updates cancellation, reallocation, usage, no-show, penalty, and utilization metrics.

### Outputs

- `Cancelled`, `Allocated`, `Used`, `NoShow`, or `Expired` status.
- Released or reallocated capacity.
- Penalty record where applicable.
- Notifications.
- Audit records.
- Reporting projections.

### Exceptions

- Terminal request cannot be cancelled through normal flow.
- HR cancellation must include reason and notify the employee.
- Late cancellation starts immediately after allocation unless tenant policy changes it.
- No-show automation must not run when no valid confirmation source exists for the tenant.

### Current Status

Core rules are documented and partially implemented across booking slices. HR cancellation and operations views are important customer-first workflows.

## Process 6: HR And Administrator Operations

### Purpose

Give privileged users the views and controls needed to operate parking without using employee screens.

### Actors

- HR user.
- Facilities user.
- Tenant administrator.
- System administrator.
- Booking.
- Customer.
- Configuration.
- Profile.
- Reporting.
- Audit.
- Notification.

### Preconditions

- User is authenticated and has a tenant-scoped privileged role.
- Tenant is configured.

### Detailed Flow

1. Privileged user opens the role-appropriate dashboard.
2. HR/facilities can see request queues, allocation outcomes, pending waitlist, cancellation/reallocation state, and next Draw time.
3. HR/facilities can run an authorized on-demand Draw when policy and operational permissions allow it.
4. HR/facilities can cancel any request or allocation within tenant scope with a required reason.
5. Tenant administrator can update tenant configuration, locations, policies, roles, and readiness-related setup.
6. System administrator can see platform/operator views that are not employee or HR defaults.
7. Sensitive actions publish business events, trigger notifications where users are affected, and write audit records.
8. Reporting gives privileged users safe operational summaries without hidden Draw internals unless the role explicitly has audit/diagnostic permission.

### Outputs

- Role-specific dashboard state.
- Manual Draw result.
- HR cancellation and employee notification.
- Configuration or tenant setup change.
- Audit evidence.
- Reporting update.

### Exceptions

- Privileged role missing or cross-tenant access attempt: deny.
- Manual Draw attempted after completed Draw: return existing result or no-op idempotently.
- HR cancellation without reason: reject.
- Employee-visible text must not expose hidden lottery seed, internal weights, raw diagnostics, or stack traces.

### Current Status

This is an active readiness area. HR and administrator defaults must differ from normal employee defaults, and next Draw visibility must be clear.

## Process 7: Reporting And Audit Evidence

### Purpose

Make the allocation process explainable, measurable, and defensible.

### Actors

- HR/facilities user.
- Tenant administrator.
- Auditor.
- Reporting.
- Audit.
- Booking.

### Preconditions

- Booking events and audit events exist for tenant activity.
- User has a role that allows the requested report or audit view.

### Detailed Flow

1. Booking publishes business events after authoritative state changes.
2. Audit consumes business events and privileged action events as append-only evidence.
3. DataHub consumes Booking outcome events and builds tenant-scoped operational projections.
4. Reporting uses approved DataHub read models plus its report catalog/configuration to serve operational reports: demand, allocation, rejection, cancellation, no-show, fairness, and utilization.
5. Tenant administrator or auditor opens audit/evidence views for specific sensitive decisions or investigations.
6. Reporting exports safe CSV output where permitted.
7. Audit exports evidence packages where permitted.
8. Data privacy rules remove or pseudonymise employee identifiers unless role policy allows named operational views.

### Outputs

- Operational reports.
- CSV exports.
- Audit trail and evidence exports.
- Fairness/utilization trend evidence.

### Exceptions

- Reporting must not expose raw lottery seed, random ordering, hidden weights, secrets, or unrelated employee-private data.
- Audit evidence must not be edited in place.
- DataHub durable read-model storage is required before customer reporting can be trusted across restart.

### Current Status

Reporting endpoints and projections exist, but the durable relational storage target is now DataHub, not Reporting. `REPORT004` should be treated as obsolete unless re-scoped to report catalog/configuration cleanup.

## Process 8: Pilot Feedback

### Purpose

Collect evaluator or customer pilot feedback without making Billing or a broad support desk a dependency.

### Actors

- Employee or pilot evaluator.
- HR/facilities user.
- Product/support operator.
- Feedback.
- Notification.
- Audit.

### Preconditions

- User is authenticated.
- Tenant is known from authenticated context.
- Feedback slice is explicitly approved.

### Detailed Flow

1. User submits feedback from Web App or Mobile App with category, message, optional context, and severity.
2. Feedback stores tenant-scoped feedback with authenticated user context.
3. Feedback avoids storing secrets, tokens, raw logs, or unrelated personal data.
4. Product/support operator or authorized tenant role reviews feedback.
5. Status or response is recorded.
6. Notification informs the user if a response is provided.
7. Audit records sensitive feedback access or status changes where required.

### Outputs

- Tenant-scoped feedback record.
- Feedback status.
- Optional response notification.
- Audit evidence for sensitive access.

### Exceptions

- Anonymous public feedback is out of scope for the first slice.
- Attachments and screenshots are out of scope unless separately threat-modeled.
- Support SLA workflow is out of scope.

### Current Status

Deferred, but reasonable as a small authenticated pilot-support slice after P0 persistence gaps are moving.

## Deferred Process: Billing And Payment

Billing is intentionally not part of the customer-first operational baseline.

Future Billing may cover tenant-level support, hosted-demo, implementation, subscription, or commercial account records. It must stay separate from employee booking details unless a later approved commercial model explicitly requires otherwise.

Do not include invoice generation, employee parking charges, payment collection, or payment gateway behavior in current customer-first ArchiMate process diagrams except as visibly future/deferred scope.

## ArchiMate Diagram Guidance

When drawing the processes, prefer these diagram layers:

| Diagram | Suggested contents |
| --- | --- |
| Tenant setup process | Customer, Identity, Configuration, Profile, Audit, Reporting readiness, tenant administrator. |
| Employee request process | Employee, Web/Mobile App, Booking, Configuration, Profile, Notification, Audit, Reporting. |
| Scheduled Draw process | Scheduler/HR trigger, Booking processor, policy/profile/capacity inputs, Draw evidence, Notification, Audit, Reporting. |
| Cancellation and reallocation process | Employee/HR cancellation, Booking reallocation, penalty, Notification, Audit, Reporting. |
| HR/admin operations process | HR, tenant administrator, system administrator views and privileged actions. |
| Reporting and audit process | Booking events, Reporting projections, Audit evidence, exports, privacy constraints. |
| Pilot feedback process | Authenticated feedback submission, review, response, notification. |

Keep Billing and Payment Gateway visually separate as future scope if they appear on a combined roadmap/process diagram.
