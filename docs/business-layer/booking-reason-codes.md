## Purpose

Booking reason codes provide stable machine-readable explanations for rejected, cancelled, expired, no-show, penalty, and manual-correction outcomes.

They are used by:

- API responses;
- audit records;
- Booking events;
- notifications;
- reporting and support views.

Employee-facing text may be localized or adjusted by channel, but the `reasonCode` must stay stable.

## Rules

- Every `Rejected`, `Cancelled`, `Expired`, `NoShow`, penalty, and manual-correction outcome must have a `reasonCode`.
- Employee-facing responses must include a clear `reasonText`.
- Technical details such as stack traces, random seeds, candidate order, and internal exception names must not appear in employee-facing `reasonText`.
- HR, admin, support, and auditor views may include deeper diagnostic references when authorized.
- New reason codes may be added over time; existing reason-code meanings must not be silently changed.

## Submission And Validation

| Reason code | Typical status | Employee-facing meaning |
| --- | --- | --- |
| `requestor_ineligible` | `Rejected` | You are not eligible to request parking for this time slot. |
| `requestor_inactive` | `Rejected` | Your profile is not active for parking requests. |
| `location_closed` | `Rejected` | This parking location is not open for requests. |
| `time_slot_closed` | `Rejected` | Requests for this time slot are closed. |
| `after_cutoff` | `Rejected` | Requests for this time slot are closed. |
| `daily_cap_exceeded` | `Rejected` | The request limit for this parking date has been reached. |
| `duplicate_overlap` | `Rejected` | You already have a request for an overlapping time slot. |
| `vehicle_required` | `Rejected` | Vehicle information is required for this request. |
| `vehicle_ineligible` | `Rejected` | The selected vehicle cannot be used for this parking request. |
| `no_matching_slot_type` | `Rejected` | No configured parking slot can satisfy this request. |
| `same_day_disabled` | `Rejected` | Same-day booking is not enabled for this location. |
| `time_slot_elapsed` | `Rejected` or `Expired` | The requested time slot is no longer actionable. |
| `no_live_capacity` | `Rejected` | No suitable parking slot is currently available. |

## Draw And Allocation

| Reason code | Typical status | Employee-facing meaning |
| --- | --- | --- |
| `allocated_by_draw` | `Allocated` | A parking slot was allocated to your request. |
| `allocated_same_day` | `Allocated` | A parking slot was allocated to your same-day request. |
| `allocated_by_reallocation` | `Allocated` | A parking slot became available and was allocated to your request. |
| `pending_waitlist_capacity_exhausted` | `Pending` | You are still waiting for a released slot because no matching capacity was available. |
| `company_car_overflow` | `Rejected` | Company-car capacity is full for this time slot. |
| `draw_failed` | Draw status | Parking allocation could not be completed automatically. |
| `draw_requires_manual_intervention` | Draw status | Parking allocation requires manual review. |

## Cancellation And Expiry

| Reason code | Typical status | Employee-facing meaning |
| --- | --- | --- |
| `cancelled_by_requestor` | `Cancelled` | Your request was cancelled. |
| `cancelled_by_hr` | `Cancelled` | Your request was cancelled by an authorized administrator. |
| `cancelled_by_system` | `Cancelled` | Your request was cancelled by the system. |
| `expired_pending_waitlist` | `Expired` | The requested time slot has passed. |
| `expired_unconfirmed_allocation` | `Expired` | The allocation is no longer actionable. |

## Usage And No-Show

| Reason code | Typical status | Employee-facing meaning |
| --- | --- | --- |
| `usage_confirmed_by_employee` | `Used` | Your parking usage was confirmed. |
| `usage_confirmed_by_hr` | `Used` | Your parking usage was confirmed by an authorized administrator. |
| `usage_confirmed_by_integration` | `Used` | Your parking usage was confirmed. |
| `usage_confirmation_late` | Manual correction | Usage confirmation was submitted after the normal confirmation window. |
| `no_show_unconfirmed` | `NoShow` | Your allocated parking slot was not confirmed as used. |

## Penalties

| Reason code | Penalty type | Employee-facing meaning |
| --- | --- | --- |
| `late_cancellation_penalty` | `lateCancellation` | A late-cancellation penalty was applied because the reservation was cancelled after allocation. |
| `no_show_penalty` | `noShow` | A no-show penalty was applied according to parking policy. |
| `manual_penalty_adjustment` | `manualAdjustment` | Your parking penalty score was adjusted by an authorized administrator. |

## Manual Correction

| Reason code | Typical use | Employee-facing meaning |
| --- | --- | --- |
| `manual_status_correction` | Status correction | Your parking request status was corrected by an authorized administrator. |
| `manual_allocation_correction` | Allocation correction | Your parking allocation was corrected by an authorized administrator. |
| `manual_usage_correction` | Usage correction | Your parking usage outcome was corrected by an authorized administrator. |
| `manual_reason_correction` | Reason correction | The explanation for your parking request was corrected by an authorized administrator. |

Manual corrections must also include the human-readable reason entered by the authorized actor. That actor-entered reason is audit-visible and may be employee-visible when appropriate.

## Authorization And Validation Failures

Authorization failures should not reveal private details about another employee's booking.

| Reason code | Typical response | Meaning |
| --- | --- | --- |
| `unauthorized` | Authorization failure | The actor is not allowed to perform this action. |
| `not_found_or_not_allowed` | Authorization or lookup failure | The requested resource was not found or is not available to the actor. |
| `concurrency_conflict` | Conflict | The request state changed before the action could be completed. |
| `invalid_status_transition` | Domain error | The requested status change is not allowed. |
| `policy_validation_failed` | Configuration error | The parking policy is incomplete or invalid. |
