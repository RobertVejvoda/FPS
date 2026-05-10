---
title: Booking Vertical Slices
---

## Purpose

Booking implementation should proceed story-by-story, not layer-by-layer. Each story cuts vertically through domain, application, API, persistence, notifications, audit, and tests where needed.

This document is the product-owner handoff for Claude implementation work.

## Implementation Model

Use vertical slice development:

```text
Story
  -> Domain objects and domain tests
  -> Application command/query and handler tests
  -> API endpoint and API tests
  -> Notifications/audit/read model updates where required
  -> Done
```

Do not build all domain objects first, then all application services, then all APIs. Each slice must be independently testable and move the product forward.

## Source Documents

Claude must follow these docs when implementing Booking:

- [Booking Request Lifecycle](./booking-request-lifecycle)
- [Executable Allocation Rules](./allocation-rules)
- [Parking Policy Configuration](./parking-policy-configuration)
- [Notification Requirements](./notification)
- [Slot Allocation Process](./process)

If implementation finds a business ambiguity, Claude should ask for clarification instead of inventing a rule.

## Implementation Rules For Claude

- Implement one slice at a time.
- Keep PRs small and tied to a single story unless the product owner explicitly approves combining stories.
- Do not implement future slices early just because a supporting class could be generalized.
- Use the documented business rules as the source of truth; code structure should serve the story, not replace the rule.
- If a business rule is missing or contradictory, stop and ask Robert or the product owner before coding the behavior.
- Update docs only when an approved requirement changes or a documented gap is closed.

## Out Of Scope For Phase 1 Booking Slices

- AI allocation optimization, demand prediction, or automated policy tuning.
- Paid parking and external payment provider integration.
- Push notifications.
- Advanced reporting and analytics beyond the query/read models named in the slices.
- Physical access integrations such as gates, card readers, camera recognition, or vendor-specific parking hardware.

## Slice B001: Submit Future Booking Request

### User Story

As an employee, I want to submit a parking request for a future time slot so that I can participate in the scheduled Draw.

### Scope

- Validate tenant, location, date, time slot, request cap, cut-off, duplicate request, requestor eligibility, vehicle requirement, and slot-type match.
- Create a booking request in `Pending` status when validation passes.
- Reject invalid requests with employee-visible reasons.
- Send mandatory in-app and email notification after successful submission or rejection.
- Write audit event for submission or rejection.

### Vertical Cut

- Domain: `BookingRequest` creation and validation result.
- Application: `SubmitBookingRequest` command and handler.
- API: `POST /bookings`.
- Persistence: save request.
- Notification: request submitted or rejected.
- Audit: request submitted or rejected.
- Tests: domain validation, command handler, API happy path, API rejection cases.

### Done Means

- Valid future request becomes `Pending`.
- Duplicate overlapping request is rejected.
- Late request after cut-off is rejected.
- Request over daily cap is rejected.
- In-app and email notification events are emitted.
- Audit event records previous/new status and reason.

### B001 Clarifications

- `Submitted` is transient. Persist `Pending` for accepted requests and `Rejected` for failed submissions.
- For requested parking date `D`, the scheduled Draw cut-off is the configured cut-off time on `D - 1` in the tenant or location policy timezone.
- Future-request submission validates configured slot type or capacity-pool compatibility only; it does not reserve live capacity or run allocation.

## Slice B002: Submit Same-Day Booking Request

### User Story

As an employee, I want to request parking for today so that I can use currently available capacity without waiting for tomorrow's Draw.

### Scope

- Validate tenant policy and same-day booking enablement.
- Apply duplicate, cap, eligibility, vehicle, and slot matching rules.
- Allocate immediately when a matching slot is available.
- Reject when same-day booking is disabled or no suitable slot exists.
- Count successful same-day allocation toward future `RecentAllocationCount`.
- Send mandatory in-app and email notification.
- Write audit event.

### Vertical Cut

- Domain: same-day allocation decision.
- Application: `SubmitBookingRequest` command path for same-day requests.
- API: `POST /bookings`.
- Persistence: save request and allocation.
- Notification: allocated or rejected.
- Audit: submission, allocation, or rejection.
- Tests: enabled/disabled same-day policy, successful immediate allocation, no capacity rejection.

### Done Means

- Same-day request allocates immediately when policy and capacity allow.
- Same-day successful allocation affects future fairness metrics.
- Same-day request does not bypass reserved-space, penalty, or slot capability policy.

## Slice B003: Cancel Pending Request

### User Story

As an employee, I want to cancel a pending request before allocation so that the Draw no longer considers it.

### Scope

- Allow `Pending -> Cancelled`.
- Do not apply late-cancellation penalty.
- Do not trigger reallocation because no slot was consumed.
- Send mandatory in-app and email notification.
- Write audit event.

### Vertical Cut

- Domain: lifecycle transition.
- Application: `CancelBooking` command.
- API: `DELETE /bookings/{id}`.
- Persistence: update request.
- Notification: cancellation.
- Audit: cancellation with no penalty.
- Tests: pending cancellation, terminal-state rejection, ownership/authorization once role matrix exists.

### Done Means

- Pending request becomes `Cancelled`.
- No penalty record is created.
- Request is excluded from future Draw processing.

## Slice B004: Run Scheduled Draw

### User Story

As HR, I want the scheduled Draw to allocate parking fairly and reproducibly so that employees receive a transparent outcome.

### Scope

- Lock Draw key at cut-off.
- Load eligible pending requests.
- Apply executable allocation rules.
- Allocate Tier 1 company-car requests first.
- Reject company-car overflow.
- Allocate remaining capacity using seeded Tier 2 weighted lottery.
- Persist allocations and rejections.
- Update metrics.
- Publish notifications.
- Write audit record with seed, algorithm version, weights, winners, assigned slots, and rejection reasons.

### Vertical Cut

- Domain: allocation algorithm and decisions.
- Application/workflow: Draw orchestration activities.
- API/admin: `POST /draws/trigger` for manual trigger.
- Persistence: requests, allocations, draw status, metrics.
- Notification: draw completed, allocated, rejected.
- Audit: full Draw attempt record.
- Tests: deterministic seeded outcomes, Tier 1 before Tier 2, overflow rejection, duplicate exclusion, idempotent replay.

### Done Means

- Same Draw key cannot create duplicate allocations on replay.
- Seeded test produces deterministic outcome.
- Each included request is allocated, rejected for a clear rule failure, or remains explicitly `Pending` as a reallocation candidate when it only lost because matching capacity was exhausted.
- Notifications and audit events are idempotent.

### B004 API Contract

`POST /draws/trigger` manually starts or replays the scheduled Draw for an authorized HR/admin actor.

Request fields:

| Field | Meaning |
| --- | --- |
| `locationId` | Location being allocated. |
| `date` | Requested parking date. |
| `timeSlot` | Time slot being allocated. |
| `reason` | Required for manual trigger outside the normal schedule. |

The Draw key is tenant, location, date, and time slot. Tenant is resolved from authenticated context, not the request body.

Response behavior:

- If no Draw exists for the key, create a Draw attempt and return accepted/started status.
- If the same Draw key is already running, return the existing Draw attempt reference.
- If the same Draw key is already completed, do not create duplicate allocations; return the completed Draw attempt reference unless the actor explicitly starts a new audited attempt through a future manual-correction flow.
- If the actor is unauthorized, reject the request.

B004 may persist draw status needed by the workflow and audit, but the employee/HR Draw status query is not part of this slice. Full `GET /draws/{date}/status` remains B009.

### B004 Outcome Rules

- Invalid, duplicate, late, ineligible, impossible-to-match, and company-car-overflow requests become `Rejected`.
- Tier 1 company-car winners become `Allocated` and do not affect Tier 2 metrics.
- Tier 2 lottery winners become `Allocated` and update `RecentAllocationCount`.
- Eligible Tier 2 requests that lose only because capacity is exhausted remain `Pending` by default for later reallocation.
- Draw completed notifications are sent to all requests included in the Draw, including pending waitlist requests.
- Rejected requests receive employee-visible reasons; pending waitlist requests receive an employee-visible message that they are still waiting for a released slot.
- Pending waitlist requests expire at the end of the requested time slot in the resolved policy timezone.
- Company-car overflow is determined before Tier 2 starts and means Tier 1 demand exceeds matching company-car-eligible capacity for the Draw key.
- The Draw attempt record must store algorithm version, seed, and ordered Tier 2 candidate sequence so B005 can reallocate from the original ordering.

B004 must not implement same-day booking, allocated-reservation cancellation, reallocation after cancellation, usage confirmation, no-show handling, manual correction, or the B009 Draw status endpoint.

## Slice B005: Cancel Allocated Reservation And Reallocate

### User Story

As an employee, I want to cancel an allocated reservation so that another eligible employee can use the released slot.

### Scope

- Allow `Allocated -> Cancelled`.
- Apply default late-cancellation penalty `+1` unless tenant policy disables or changes it.
- Release the slot.
- Automatically reallocate to next eligible pending request when one exists.
- Notify original and new requestor.
- Write audit events for cancellation, penalty, release, and reallocation.

### Vertical Cut

- Domain: lifecycle transition, penalty trigger, reallocation decision.
- Application: `CancelBooking` command path for allocated request.
- API: `DELETE /bookings/{id}`.
- Persistence: request, allocation, penalty, reallocation.
- Notification: cancellation, penalty, reallocation.
- Audit: all related transitions.
- Tests: late penalty, reallocation success, no eligible request fallback, idempotent duplicate cancellation.

### Done Means

- Allocated reservation becomes `Cancelled`.
- Penalty is recorded according to policy.
- Released slot is reassigned when eligible pending request exists.
- Both affected users receive in-app and email notifications.

### B005 Reallocation Rules

When an allocated reservation is cancelled, FPS must:

1. Verify the actor can cancel the reservation.
2. Move the original request from `Allocated` to `Cancelled`.
3. Release the assigned slot.
4. Apply the configured late-cancellation penalty unless tenant policy disables it or the requestor is exempt.
5. Find eligible `Pending` requests for the same tenant, location, date, and time slot.
6. Filter candidates to requests that match the released slot's constraints.
7. Reallocate to the next eligible candidate using the original Draw ordering when available; if no recorded ordering exists, use the same deterministic weighted-selection rules and record the seed/decision.
8. Notify and audit the original requestor and any new requestor.

If no eligible pending request exists, the released slot remains available for same-day allocation or manual operational use under tenant policy. The original cancellation and penalty still stand.

Late cancellation starts immediately after a request becomes `Allocated`; v1 has no additional hours-before-start threshold. Booking owns the v1 penalty ledger for booking-related penalties. Penalty creation must be idempotent by source event ID and penalty type.

Reallocation uses original Draw ordering when the Draw attempt for the same tenant, location, date, and time slot has a recorded algorithm version, seed, and ordered Tier 2 candidate sequence. Skip candidates that are no longer `Pending`, are no longer eligible, or do not match the released slot. If ordering is missing or corrupt, run a deterministic fallback selection, record the fallback seed/decision, and audit why fallback was used.

Notification events are asynchronous after booking state changes are persisted. Notification failure must not roll back cancellation, penalty, or reallocation outcomes.

### B005 API Contract

`DELETE /bookings/{id}` uses the same endpoint as B003 but adds the `Allocated` cancellation path.

Behavior:

- `Pending` cancellation remains the B003 behavior: cancel without penalty or reallocation.
- `Allocated` cancellation applies B005 behavior: cancel, release, penalty, and reallocation attempt.
- Terminal statuses cannot be cancelled again.
- The command must be idempotent: retrying the same cancellation must not apply duplicate penalties, duplicate slot releases, duplicate reallocations, or duplicate notifications.

B005 must not implement Draw execution, same-day booking, usage confirmation, no-show handling, manual correction, or the B009 Draw status endpoint.

## Slice B006: Confirm Usage

### User Story

As an employee or authorized confirmation source, I want to confirm parking usage so that FPS records actual utilization.

### Scope

- Allow `Allocated -> Used`.
- Record confirmation source.
- Respect tenant confirmation policy.
- Send mandatory in-app and email notification.
- Write audit event.

### Vertical Cut

- Domain: usage confirmation transition.
- Application: `ConfirmSlotUsage` command.
- API: confirmation endpoint.
- Persistence: usage confirmation.
- Notification: usage confirmed.
- Audit: confirmation source and timestamp.
- Tests: valid confirmation, invalid non-allocated confirmation, late manual correction path.

### Done Means

- Allocated request becomes `Used`.
- Confirmation source is recorded.
- Metrics/reporting can distinguish used from merely allocated.

## Slice B007: Mark No-Show

### User Story

As HR, I want FPS to mark no-shows when usage is not confirmed so that fairness and reporting reflect unused reservations.

### Scope

- Run no-show evaluation only when tenant no-show detection is enabled.
- Require a configured usage confirmation method.
- Allow `Allocated -> NoShow`.
- Apply default no-show penalty `+2` unless tenant policy disables or changes it.
- Send mandatory in-app and email notification.
- Write audit event.

### Vertical Cut

- Domain: no-show transition and penalty trigger.
- Application: scheduled or manual no-show evaluation command.
- Persistence: request status and penalty.
- Notification: no-show and penalty.
- Audit: no-show decision.
- Tests: no-show enabled, no-show disabled, missing confirmation method, penalty creation.

### Done Means

- FPS never marks no-show automatically when usage confirmation is unavailable.
- No-show penalty affects future allocation probability.

## Slice B008: View My Bookings

### User Story

As an employee, I want to view my booking requests and outcomes so that I understand my parking status.

### Scope

- Return current user's requests and allocations.
- Include status, date/time slot, location, slot where allocated, reason where rejected/cancelled/no-show, and next action when available.
- Do not expose other employees' details.
- Support optional filters by date range and status.
- Return newest relevant bookings first, with stable pagination.

### Vertical Cut

- Query/read model: user-scoped booking list.
- API: `GET /bookings`.
- Tests: user scoping, status mapping, no cross-user leakage.

### Done Means

- Employee sees only own bookings.
- Statuses match [Booking Request Lifecycle](./booking-request-lifecycle).
- Employee-visible reasons match lifecycle and notification docs.
- Default response includes future bookings plus recent history from the tenant allocation lookback window.
- Results are ordered by requested date descending, then creation time descending.
- Pagination is stable and does not leak total counts for other users.

### B008 API Contract

`GET /bookings` returns bookings for the authenticated user only. The request body must not contain user or tenant identity; tenant and user are resolved from the authenticated context.

Optional query parameters:

| Parameter | Meaning | Default |
| --- | --- | --- |
| `from` | Inclusive requested parking date lower bound. | Today minus tenant `allocationLookbackDays`. |
| `to` | Inclusive requested parking date upper bound. | No explicit upper bound for future bookings. |
| `status` | Optional canonical booking status filter. | All statuses. |
| `pageSize` | Maximum items to return. | `50`, with a hard maximum of `100`. |
| `cursor` | Opaque continuation token from previous response. | First page. |

Each returned booking item must include:

- booking request ID;
- requested date;
- time slot;
- location;
- current status;
- employee-visible reason when the status is `Rejected`, `Cancelled`, `NoShow`, or `Expired`;
- allocated slot or slot label when status is `Allocated` or `Used` and the slot may be shown to the employee;
- next action when one exists, such as `cancel`, `confirmUsage`, or `none`;
- created timestamp;
- last status change timestamp.

The default `from` value uses the resolved parking policy for the request's tenant and location. If no override is configured, the tenant default applies; if no explicit value is configured, use the documented default `allocationLookbackDays = 10`.

`nextAction` must only advertise actions that are currently implemented and allowed for the authenticated user. For B008, after B001 and B003 are merged, the only expected actionable value is `cancel` for a cancellable `Pending` request. `confirmUsage` is reserved for the later usage-confirmation slice and must not be returned until that action exists. Allocated-reservation cancellation must not be advertised until B005 implements it.

The response must not include:

- other employees' names, IDs, weights, penalties, or request details;
- lottery seed or internal algorithm diagnostics;
- audit-only fields;
- hidden slot metadata.

## Slice B009: View Draw Status

### User Story

As HR, I want to view Draw status and results so that I can explain allocation outcomes and handle exceptions.

### Scope

- Return Draw status by tenant, location, date, and time slot.
- Include request counts, allocation counts, rejection counts, failure state, completion time, and summary reasons.
- HR/auditor view may include weights, seed, and audit references.
- Employee view must not expose other employees or audit-only diagnostics.

### Vertical Cut

- Query/read model: Draw status and result summary.
- API: `GET /draws/{date}/status`.
- Tests: HR visibility, employee redaction, failed/in-progress/completed statuses.

### Done Means

- HR can explain high-level outcomes.
- Sensitive implementation details are limited to authorized roles.

## Slice B010: Manual Correction

### User Story

As HR, I want to apply a justified manual correction so that exceptional business cases can be fixed without hiding the change.

### Scope

- Allow authorized manual correction for request status, allocation, penalty, or usage outcome.
- Require reason.
- Write append-only audit event.
- Send mandatory in-app and email notification to affected requestor.
- Do not mutate old audit records.

### Vertical Cut

- Domain/application: manual correction command.
- API/admin: manual correction endpoint.
- Persistence: correction event and affected state.
- Notification: manual correction.
- Audit: old value, new value, actor, reason.
- Tests: reason required, unauthorized rejected, notification sent, audit immutable.

### Done Means

- Manual corrections are visible, auditable, and explainable.
- Normal lifecycle rules remain intact.

## Implementation Order

Recommended order:

1. B001 Submit Future Booking Request
2. B003 Cancel Pending Request
3. B008 View My Bookings
4. B004 Run Scheduled Draw
5. B005 Cancel Allocated Reservation And Reallocate
6. B002 Submit Same-Day Booking Request
7. B006 Confirm Usage
8. B007 Mark No-Show
9. B009 View Draw Status
10. B010 Manual Correction

This order gives Claude a small first slice, then adds read visibility, then tackles Draw complexity once request lifecycle basics are stable.

## Cross-Cutting Done Criteria

Every slice must:

- include domain tests where domain behavior changes;
- include application handler tests for commands or queries;
- include API tests when an endpoint is added or changed;
- publish required domain events;
- preserve tenant isolation;
- use tenant policy resolution where policy affects behavior;
- create audit events for business state changes;
- emit mandatory notification events for critical operational outcomes;
- avoid implementing future features not required by the slice.
