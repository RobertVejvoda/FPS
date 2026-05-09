---
title: Executable Allocation Rules
---

## Purpose

This document turns the allocation business policy into executable rules for the Draw implementation. If this document conflicts with a higher-level process description, this document wins for implementation until the decision log says otherwise.

## Scope

These rules cover:

- scheduled Draw allocation for future time slots;
- same-day immediate allocation;
- cancellation reallocation;
- allocation metrics used by Tier 2 weighting;
- penalty defaults;
- audit data required to explain allocation outcomes.

## Terms

| Term | Meaning |
| --- | --- |
| Draw key | Tenant, location, date, and time slot being allocated. |
| Eligible request | A request that passes tenant policy, duplicate, time slot, vehicle, and capacity constraints. |
| Matching slot | A slot that can satisfy the request's location, time, vehicle, accessibility, EV, motorcycle, reserved, or company-car requirements. |
| Tier 1 | Company-car allocation tier. |
| Tier 2 | Weighted lottery tier for remaining eligible non-company-car requests. |
| RecentAllocationCount | Successful non-company-car allocations in the tenant lookback window, including same-day allocations. |
| ActivePenaltyScore | Active penalty points that affect allocation probability. |

## Request Validation

FPS rejects a request before allocation when any of these conditions is true:

- the requestor is not eligible under tenant policy;
- the requested date or time slot is closed;
- the request would exceed the tenant's 500-request cap for the date;
- the request is a duplicate;
- the vehicle or request requirements cannot be matched to any configured slot type;
- the request is submitted after the configured cut-off for a scheduled Draw.

A duplicate request is one with the same tenant, same requestor, same date, and an overlapping time slot. Vehicle and location do not make overlapping requests distinct.

## Allocation Precedence

The Draw applies rules in this order:

1. Resolve tenant, location, date, and time slot.
2. Exclude invalid, duplicate, late, and ineligible requests.
3. Resolve matching slots and reserved-space constraints.
4. Allocate Tier 1 company-car requests.
5. Reject Tier 1 overflow when company-car requests exceed available matching capacity.
6. Allocate remaining eligible requests through Tier 2 weighted lottery.
7. Persist allocations and rejections.
8. Update user metrics.
9. Publish notifications and audit events.

## Tier 1 Company-Car Allocation

Company-car requests are allocated before the Tier 2 lottery.

Rules:

- `HasCompanyCar = true` places the request in Tier 1.
- Tier 1 requests do not participate in the weighted lottery.
- Tier 1 allocations do not increment `RecentAllocationCount`.
- Tier 1 requestors do not receive penalties for company-car allocations.
- If matching capacity is insufficient, FPS rejects overflow requests for now.

Overflow is expected to indicate tenant configuration drift, not normal business demand. The rejection reason must make that visible to HR.

## Tier 2 Weighted Lottery

Tier 2 runs after Tier 1 has consumed matching capacity.

The default weight is:

```text
Tier2Weight = 1 / (1 + RecentAllocationCount + ActivePenaltyScore)
```

Rules:

- `RecentAllocationCount` uses the tenant-configured lookback window.
- The default lookback window is `10` days.
- Same-day successful allocations count toward `RecentAllocationCount`.
- Rejected requests do not reduce weight.
- Every eligible Tier 2 request has a non-zero weight unless tenant policy excludes it before the lottery.
- The lottery selects without replacement until capacity is exhausted or no eligible request remains.

## Slot Matching

A request can only win a slot that satisfies its constraints.

Slot matching must consider:

- tenant and location;
- date and time slot;
- vehicle type;
- motorcycle capacity;
- EV charging requirement;
- accessibility requirement;
- reserved-space or company-car restrictions;
- slot availability after previous allocations in the same Draw.

When multiple matching slots are available for a winning request, FPS should choose the most constrained suitable slot first. This preserves flexible slots for later requests.

## Randomness and Reproducibility

Every Draw must use a recorded random seed.

Rules:

- the seed is generated once per Draw key;
- manual re-run of the same Draw key must reuse the existing seed unless an admin explicitly starts a new audited Draw attempt;
- the audit record stores the seed and algorithm version;
- test fixtures may inject the seed to make outcomes deterministic.

The product does not need to expose the seed to employees, but HR and audit roles must be able to reproduce or explain the result.

## Same-Day Allocation

Same-day allocation happens after the scheduled Draw has already run.

Rules:

- same-day requests still pass tenant policy, duplicate, vehicle, and slot matching checks;
- if a suitable slot is available, FPS allocates it immediately;
- if no suitable slot exists, FPS rejects the request or places it on a waitlist when that tenant feature exists;
- successful same-day allocations count toward `RecentAllocationCount`;
- same-day allocation does not bypass penalties or reserved-space constraints.

## Cancellation Reallocation

When an allocated reservation is cancelled, FPS releases the slot and automatically reallocates it to the next eligible requestor when one exists.

Rules:

- cancellation before allocation removes the request from the queue;
- cancellation after allocation releases the slot;
- late cancellation applies the configured penalty when tenant policy says so;
- reallocation uses the remaining eligible requests for the same tenant, location, date, and time slot;
- the reallocated request must match the released slot's constraints;
- the cancellation and reallocation are separate audited events;
- both affected requestors receive notifications.

If no eligible requestor exists, the slot remains available for same-day allocation or manual use under tenant policy.

## Penalties

Default penalty points:

| Event | Default score |
| --- | ---: |
| Late cancellation after allocation | `+1` |
| Confirmed no-show | `+2` |
| Manual HR adjustment | Configurable signed value |

Penalty rules:

- penalty settings are tenant-configurable;
- manual adjustments require a reason;
- penalties must have an effective date;
- penalties expire according to tenant policy;
- if no tenant expiry is configured, the allocation lookback window applies;
- company-car Tier 1 allocations do not create penalties.

## Metrics Update

After allocation is persisted:

- increment `RecentAllocationCount` for successful Tier 2 allocations;
- increment `RecentAllocationCount` for successful same-day allocations;
- do not increment it for Tier 1 company-car allocations;
- update `ActivePenaltyScore` only from active penalty records;
- never derive penalties from rejected requests.

The Draw should use a draw-time snapshot of metrics. Later metric updates must not change already completed Draw outcomes.

## Audit Record

Each Draw attempt must record:

- tenant, location, date, and time slot;
- algorithm version;
- random seed;
- request IDs considered;
- eligibility result per request;
- rejection reason per rejected request;
- metrics snapshot per eligible request;
- calculated Tier 2 weight per eligible Tier 2 request;
- selected winners;
- assigned slot IDs;
- cancellation/reallocation events when applicable;
- timestamps and actor for manual trigger or manual adjustment.

Audit records must be append-only. Corrections require a new event, not mutation of the old one.

## Failure and Idempotency

The Draw workflow must be idempotent for a Draw key.

Rules:

- a completed Draw key must not create duplicate allocations when replayed;
- each activity can be retried safely;
- persisted allocations are the source of truth after `PersistAllocationsActivity`;
- notification publishing must be idempotent;
- partial failure must be recoverable by replay or compensation;
- manual re-run after completion requires a new audited attempt.

## Implementation Notes

- Keep the allocation algorithm in the domain layer and free of Dapr, MongoDB, or HTTP dependencies.
- Inject randomness as an abstraction so tests can use fixed seeds.
- Unit tests should cover Tier 1, Tier 2 weights, duplicate rejection, same-day metric updates, company-car overflow, cancellation reallocation, and deterministic seeded outcomes.
