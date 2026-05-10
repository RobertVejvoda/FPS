---
title: Booking Context Contract
---

## Purpose

This document turns the Booking relationships shown in the [Exchange Map](../business-layer#exchange-map) into implementation boundaries. It defines what Booking may consume from other business domains, what Booking must publish, and which data must not cross boundaries.

Read this with [Booking Vertical Slices](./booking-vertical-slices), [Booking Event Contracts](./booking-event-contracts), [Booking Authorization](./booking-authorization), and [Booking API Contract](./booking-api-contract).

## Boundary Principle

Booking owns parking request lifecycle, allocation decisions, booking status, and booking history. Other domains may provide inputs or receive outcomes, but they must not directly decide Booking state transitions.

Rules:

- Booking must use explicit contracts for cross-domain data.
- Booking must not read another domain's database tables or collections directly.
- Booking may keep local snapshots needed for deterministic decisions.
- Booking decisions must record the snapshot version, policy version, or source event that influenced the decision when the information affects allocation, rejection, penalty, or audit.
- Cross-domain failures must not create partial Booking state. If required input is unavailable, the command must fail safely or defer until the required input is available.

## Inbound Dependencies

### Configuration To Booking

Configuration provides policy inputs used by request validation, same-day allocation, scheduled Draw, expiry, cancellation, and usage confirmation.

Booking may consume:

- tenant booking policy;
- location booking policy;
- time slot definitions;
- capacity pools and slot capability rules;
- Draw cut-off time and policy timezone;
- allocation lookback days;
- Tier 2 default and maximum weight settings;
- same-day booking settings;
- cancellation and no-show policy settings.

Booking must snapshot the effective policy version for decisions that affect persisted outcomes. A later policy change must not silently rewrite the reason for an existing booking outcome.

### Profile To Booking

Profile provides requestor eligibility and capability inputs.

Booking may consume:

- requestor identity reference;
- employment or tenant membership status;
- parking eligibility;
- vehicle requirement or vehicle capability data needed for slot matching;
- company-car entitlement when policy treats company cars as Tier 1;
- accessibility or reserved-space eligibility when policy requires it.

Booking must not expose Profile private data in employee-facing Booking responses. Booking may store only the minimum snapshot needed to explain and audit the decision.

### System To Booking

System or scheduler may trigger scheduled operational work.

Booking may receive:

- scheduled Draw trigger;
- waitlist expiry trigger;
- no-show evaluation trigger;
- retry of an idempotent workflow step.

System-triggered commands must be idempotent and auditable. A repeated trigger must not duplicate allocations, penalties, notifications, or audit records.

## Outbound Responsibilities

### Booking To Notification

Booking raises notification intents after authoritative state changes are persisted.

Booking must publish notification events for:

- request accepted as `Pending`;
- request rejected;
- request cancelled;
- booking allocated;
- booking lost but remains waitlisted;
- booking expired from waitlist;
- allocated booking cancelled;
- reallocation outcome;
- usage confirmed;
- no-show recorded.

Notification failure must not roll back a persisted Booking state change. The failed notification must be retryable and traceable by correlation ID.

### Booking To Audit

Booking publishes audit events for all meaningful state transitions and allocation decisions.

Audit events must include:

- tenant;
- actor or system source;
- booking request ID;
- previous status and new status when applicable;
- reason code;
- source command or source event ID;
- correlation ID;
- effective timestamp;
- policy version or snapshot reference when policy affected the decision.

Draw audit must additionally include the algorithm version, seed, candidate ordering, winners, rejected candidates, and fallback decisions as documented in [Booking Vertical Slices](./booking-vertical-slices).

### Booking To Reporting

Reporting consumes Booking read models or events. Reporting must not drive Booking state.

Booking may provide:

- booking request status changes;
- allocation outcomes;
- cancellation outcomes;
- no-show and usage confirmation outcomes;
- anonymized or aggregated demand metrics where required.

Reporting must respect tenant isolation and privacy rules. Employee self-service reporting must not expose another employee's private Booking data.

## Synchronous Versus Async Rules

Booking may synchronously query Configuration and Profile only when the data is required to make the command decision now. Examples: validating a new request, determining company-car Tier 1 eligibility, or resolving policy timezone.

Booking should use async events for Notification, Audit, and Reporting because those domains observe Booking outcomes. Their failure must not change the Booking outcome.

## Anti-Corruption Rules

Booking should translate external concepts into Booking language before using them in domain logic.

Examples:

| External concept | Booking concept |
| --- | --- |
| Profile company-car entitlement | Tier 1 company-car eligibility |
| Profile vehicle attributes | Slot matching capabilities |
| Configuration time slot | Booking requested period |
| Configuration capacity pool | Candidate allocation pool |
| Scheduler trigger | Draw or expiry command |

Booking domain logic must not depend on UI labels, raw JWT claims, notification templates, report schemas, or provider-specific scheduler payloads.

## Failure Rules

| Dependency | Failure behavior |
| --- | --- |
| Configuration unavailable for command validation | Fail safely; do not accept or allocate without policy. |
| Profile unavailable for eligibility validation | Fail safely; do not accept or allocate without eligibility. |
| Notification unavailable after state change | Persist state; publish retryable notification failure. |
| Audit unavailable for auditable command | Persist audit through the same reliable outbox path as the state change, or fail before state mutation if reliable audit cannot be guaranteed. |
| Reporting unavailable | Persist Booking state; reporting catches up from events/read model rebuild. |

## Implementation Notes For Claude

- Keep cross-domain access behind application interfaces owned by Booking.
- Do not reference other bounded contexts' persistence models from Booking domain code.
- Tests for a vertical slice should fake Configuration, Profile, Notification, Audit, and Reporting through Booking-owned interfaces.
- If an external dependency needs more data than listed here, stop and ask before widening the contract.
