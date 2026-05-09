---
title: Booking Request Lifecycle
---

## Purpose

This document defines the lifecycle of a parking booking request from submission to final outcome. It is written as an implementation contract for the Booking domain and should be read together with [Executable Allocation Rules](./allocation-rules).

## Scope

These rules cover:

- request submission;
- status names and meanings;
- valid and invalid status transitions;
- cancellation behavior;
- late-cancellation penalty trigger;
- usage confirmation;
- no-show handling;
- reallocation after cancellation;
- employee-visible outcome reasons.

## Canonical Statuses

| Status | Meaning | Terminal |
| --- | --- | --- |
| `Submitted` | Request was received but not yet accepted into the allocation queue. | No |
| `Pending` | Request passed initial validation and is waiting for Draw, same-day allocation, cancellation, or rejection. | No |
| `Allocated` | Request has an assigned parking slot. | No |
| `Rejected` | Request cannot be fulfilled or is invalid. | Yes |
| `Cancelled` | Request or allocation was cancelled by the requestor, HR, or the system. | Yes |
| `Used` | Allocation was confirmed as used. | Yes |
| `NoShow` | Allocation was not used and tenant policy marks it as a no-show. | Yes |
| `Expired` | Request or allocation passed its useful time window without a final business outcome. | Yes |

Implementation may use different internal enum names only if API responses, audit records, and documentation keep these business meanings clear.

## Status Transitions

| From | To | Trigger |
| --- | --- | --- |
| `Submitted` | `Pending` | Initial validation passes. |
| `Submitted` | `Rejected` | Initial validation fails. |
| `Pending` | `Allocated` | Draw, same-day allocation, or cancellation reallocation assigns a slot. |
| `Pending` | `Rejected` | Request is duplicate, ineligible, late, over cap, impossible to match, or no capacity remains. |
| `Pending` | `Cancelled` | Requestor or authorized role cancels before allocation. |
| `Pending` | `Expired` | Requested time passes before allocation. |
| `Allocated` | `Used` | Usage is confirmed. |
| `Allocated` | `Cancelled` | Requestor, HR, or system cancels after allocation. |
| `Allocated` | `NoShow` | Usage is not confirmed within tenant policy. |
| `Allocated` | `Expired` | Allocation passes expiry window where no-show policy is not enabled. |

Invalid transitions must be rejected with a clear domain error. Terminal statuses cannot transition to another status except through an explicit audited manual correction flow.

## Submission Rules

FPS accepts a booking request only when:

- the requestor is active and eligible under tenant policy;
- the tenant, location, date, and time slot are open for requests;
- the request does not exceed the tenant's daily request cap;
- the request is not a duplicate;
- the requestor has a registered or provided vehicle when vehicle data is required;
- the vehicle and request constraints can be matched to at least one configured slot type.

The duplicate definition is owned by [Executable Allocation Rules](./allocation-rules): same tenant, same requestor, same date, and overlapping time slot.

## Cancellation Rules

Before allocation:

- cancellation moves the request from `Pending` to `Cancelled`;
- no late-cancellation penalty applies;
- no reallocation is needed because no slot was consumed.

After allocation:

- cancellation moves the request from `Allocated` to `Cancelled`;
- the slot is released;
- FPS automatically attempts reallocation to the next eligible requestor;
- the cancellation and any reallocation must be audited;
- affected requestors receive notifications.

Late cancellation starts after a slot has been allocated. The default late-cancellation penalty is `+1`, unless tenant policy disables or changes it.

## Reallocation Rules

Reallocation is a source of allocation, not a separate final status.

Rules:

- the receiving request moves from `Pending` to `Allocated`;
- audit records and notifications must indicate that the allocation came from cancellation reallocation;
- the reallocated request must satisfy the released slot's constraints;
- if no eligible request exists, the slot remains available for same-day allocation or manual use under tenant policy.

## Usage Confirmation Rules

Usage confirmation proves that an allocated slot was actually used.

Rules:

- only `Allocated` requests can become `Used`;
- usage may be confirmed by employee self-confirmation, HR, QR code, access-control integration, license plate recognition, or another tenant-configured signal;
- confirmation must identify the source of confirmation;
- confirmation after the tenant's allowed confirmation window requires an audited manual correction;
- company-car allocations may still be confirmed for utilization reporting even though they do not affect Tier 2 weight.

## No-Show Rules

A request becomes `NoShow` when:

- it was `Allocated`;
- no valid usage confirmation exists within the tenant's configured confirmation window;
- tenant policy enables no-show detection for that confirmation method.

The default no-show penalty is `+2`. If usage confirmation is not available for a tenant, FPS must not automatically mark no-shows; it may only report unconfirmed usage as unknown.

## Expiry Rules

`Expired` is used when a request or allocation is no longer actionable but should not be treated as rejected, cancelled, used, or no-show.

Examples:

- a pending same-day request reaches the end of the requested time slot without allocation;
- an allocated request passes its usage window in a tenant where no-show detection is disabled.

Expiry must be auditable and must not create penalties by default.

## Employee-Visible Reasons

FPS must provide clear outcome reasons for employee-facing statuses.

| Outcome | Example reason |
| --- | --- |
| `Rejected` | No matching parking slot is available for your request. |
| `Rejected` | You already have a request for an overlapping time slot. |
| `Rejected` | Requests for this time slot are closed. |
| `Rejected` | Company-car capacity is full for this time slot. |
| `Cancelled` | Your request was cancelled. |
| `Allocated` | A parking slot was allocated to your request. |
| `Allocated` by reallocation | A parking slot became available and was allocated to your request. |
| `Used` | Your parking usage was confirmed. |
| `NoShow` | Your allocated parking slot was not confirmed as used. |
| `Expired` | The requested time slot has passed. |

Technical details such as random seeds, internal rule IDs, or stack traces must not be shown to employees. HR, audit, and support roles may see deeper diagnostic detail according to role permissions.

## Audit Requirements

Each lifecycle transition must record:

- tenant;
- request ID;
- previous status;
- new status;
- actor or system source;
- timestamp;
- reason code;
- human-readable reason;
- related slot ID where applicable;
- related allocation or reallocation event where applicable;
- penalty impact where applicable.

Audit records must be append-only. Manual correction creates a new audit event rather than mutating old history.

## Acceptance Criteria For Implementation

- Given a valid future request, when it is submitted before cut-off, then it becomes `Pending`.
- Given a duplicate overlapping request, when it is submitted, then it becomes `Rejected` with a duplicate reason.
- Given a `Pending` request, when the requestor cancels before allocation, then it becomes `Cancelled` with no penalty.
- Given an `Allocated` request, when the requestor cancels, then it becomes `Cancelled`, receives the default late-cancellation penalty, releases the slot, and triggers reallocation.
- Given a released slot and an eligible pending request, when reallocation runs, then the pending request becomes `Allocated` and notifications are sent.
- Given an `Allocated` request, when valid usage confirmation is received, then it becomes `Used`.
- Given an `Allocated` request with no valid usage confirmation and no-show policy enabled, when the confirmation window closes, then it becomes `NoShow` and receives the default no-show penalty.
- Given a terminal request, when another normal lifecycle transition is attempted, then FPS rejects the transition with a clear domain error.
