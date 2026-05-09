---
title: Parking Policy Configuration
---

## Purpose

This document defines which parking policy settings a customer tenant can configure. These requirements support the booking lifecycle and allocation rules without requiring product code changes for normal customer policy differences.

## Configuration Model

FPS uses tenant-level defaults with optional per-location overrides.

Rules:

- each tenant has one default parking policy;
- each location may override selected policy fields;
- when a location override exists, it wins over the tenant default for that location;
- when no location override exists, FPS uses the tenant default;
- policy changes must be audited;
- policy changes affect future requests and future Draws only, unless an authorized role explicitly reprocesses an existing Draw attempt.

## Policy Scope

A parking policy applies to:

- request submission;
- scheduled Draw cut-off;
- same-day booking;
- request caps;
- allocation lookback window;
- penalties and expiry;
- usage confirmation and no-show detection;
- slot capabilities;
- company-car and reserved-space behavior;
- employee-visible rejection reasons.

## Tenant Default Policy

Each tenant policy must define these fields.

| Field | Default | Notes |
| --- | --- | --- |
| `timeZone` | Tenant business timezone | Used for cut-off, Draw schedule, and date boundaries. |
| `drawCutOffTime` | `18:00` local time | Requests after this time are late for the scheduled Draw. |
| `drawSchedule` | At cut-off time | Draw starts when cut-off is reached unless manually triggered. |
| `dailyRequestCap` | `500` | Maximum requests per tenant/date. |
| `allocationLookbackDays` | `10` | Used by `RecentAllocationCount` and default penalty expiry. |
| `lateCancellationPenalty` | `1` | Applies after a slot has been allocated. |
| `noShowPenalty` | `2` | Applies when no-show policy marks an allocation unused. |
| `manualAdjustmentEnabled` | `true` | Manual adjustments require reason and audit record. |
| `sameDayBookingEnabled` | `true` | Allows immediate allocation after Draw. |
| `sameDayUsesRequestCap` | `true` | Same-day requests count toward the same date cap. |
| `automaticReallocationEnabled` | `true` | Released allocated slots are reassigned to the next eligible requestor. |
| `usageConfirmationRequired` | `false` | Tenant can enable confirmation when a reliable method exists. |
| `usageConfirmationWindowMinutes` | `0` | `0` means not enforced unless confirmation is enabled. |
| `noShowDetectionEnabled` | `false` | Must not be enabled without a confirmation method. |
| `companyCarTier1Enabled` | `true` | Company-car requests are allocated before Tier 2. |
| `companyCarOverflowBehavior` | `reject` | First implementation rejects overflow. |

## Location Overrides

A location may override:

- timezone;
- Draw cut-off time;
- Draw schedule;
- daily request cap;
- allocation lookback days;
- same-day booking enablement;
- usage confirmation requirement and window;
- no-show detection;
- penalty values and expiry;
- automatic reallocation enablement;
- company-car overflow behavior when future product versions support more options;
- supported slot capabilities.

Location overrides must not silently remove required tenant-wide compliance, audit, or role-based access controls.

## Slot Capability Configuration

Each location must define its parking slots or capacity pools.

A slot or capacity pool may define:

- slot ID or pool ID;
- location;
- time-slot availability;
- supported vehicle types;
- EV charging availability;
- accessibility availability;
- motorcycle capacity;
- company-car-only restriction;
- reserved user or reserved group;
- active/inactive status.

FPS must not allocate a request to a slot that does not satisfy the request's configured constraints.

## Reserved-Space Policy

Reserved spaces can be assigned to users or groups, such as company-car users, accessibility needs, executives, or operational roles.

Rules:

- reserved users keep priority access when policy grants it;
- reserved users should still declare when they need the space;
- released reserved spaces may be allocated to other eligible requestors when policy allows;
- reserved-space decisions must be visible in audit and reporting;
- unused reserved capacity should not stay hidden when policy allows reuse.

## Company-Car Policy

Default company-car policy:

- company-car requests use Tier 1 allocation;
- Tier 1 allocation happens before Tier 2 lottery;
- Tier 1 allocations do not affect `RecentAllocationCount`;
- Tier 1 allocations do not create penalties;
- company-car overflow is rejected for now.

The default overflow rejection reason should indicate configuration drift: company-car demand exceeded matching capacity.

## Penalty Policy

Default penalties:

| Penalty | Default score | Default expiry |
| --- | ---: | --- |
| Late cancellation after allocation | `1` | Allocation lookback window |
| Confirmed no-show | `2` | Allocation lookback window |
| Manual adjustment | Configured per adjustment | Explicit expiry or allocation lookback window |

Rules:

- tenant policy may change scores and expiry;
- manual adjustments require a signed score, reason, actor, effective date, and expiry;
- penalties affect future allocation probability only;
- penalties must be auditable;
- rejected requests never create penalties.

## Usage Confirmation Policy

Usage confirmation is optional by default.

Supported confirmation methods may include:

- employee self-confirmation;
- HR manual confirmation;
- QR code scan;
- card reader;
- access-control integration;
- license plate recognition.

Rules:

- no-show detection requires usage confirmation to be enabled;
- if no confirmation method is configured, unconfirmed usage remains unknown rather than no-show;
- confirmation source must be recorded;
- manual correction must be audited.

## Policy Change Behavior

Policy changes must be predictable.

Rules:

- changes apply to new requests immediately after publication;
- changes apply to future Draws that have not started;
- changes do not mutate completed Draws;
- changes do not alter already assigned allocations unless an authorized role performs an audited manual correction;
- changing penalties does not rewrite historical penalty records unless an explicit correction is made;
- every policy publication creates an audit record with old value, new value, actor, timestamp, and reason when provided.

## Acceptance Criteria For Implementation

- Given a tenant with no location override, when FPS evaluates a request, then it uses tenant default policy values.
- Given a location override, when FPS evaluates a request for that location, then overridden fields use the location value and all other fields fall back to tenant defaults.
- Given a request after the configured cut-off, when scheduled Draw submission is evaluated, then FPS rejects it as late.
- Given same-day booking is disabled, when an employee submits a same-day request, then FPS rejects it with a clear reason.
- Given usage confirmation is disabled, when an allocation is not confirmed, then FPS must not automatically mark it as no-show.
- Given no-show detection is enabled without a confirmation method, when the policy is published, then FPS rejects the policy as invalid.
- Given a policy change, when it is published, then FPS records an audit event with changed fields.
- Given a completed Draw, when policy changes later, then the completed Draw outcome remains unchanged.
