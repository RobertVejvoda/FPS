## Purpose

This document defines which parking policy settings a customer tenant can configure. These requirements support the booking lifecycle and allocation rules without requiring product code changes for normal customer policy differences.

## Configuration Model

FairSpot uses tenant-level defaults with optional per-location overrides.

Rules:

- each tenant has one default parking policy;
- each location may override selected policy fields;
- when a location override exists, it wins over the tenant default for that location;
- when no location override exists, FairSpot uses the tenant default;
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

For a future parking request, `drawCutOffTime` is evaluated against the requested parking date. The default rule is: parking date `D` closes at the configured cut-off time on calendar date `D - 1` in the policy timezone. Location timezone overrides tenant timezone when a location override exists.

Scheduled Draw execution remains the normal operating model. An on-demand Draw trigger may exist for local demo, controlled operations, recovery, or support scenarios, but it does not change the policy schedule. Manual/on-demand execution must be role-restricted, explicit about location/date/time slot, reasoned, audited, and idempotent for the same Draw key.

## Tenant Default Policy

Each tenant policy must define these fields.

| Field | Default | Notes |
| --- | --- | --- |
| `timeZone` | Tenant business timezone | Used for cut-off, Draw schedule, and date boundaries. |
| `drawCutOffTime` | `18:00` local time | Requests after this time are late for the scheduled Draw. |
| `drawSchedule` | At cut-off time | Draw starts when cut-off is reached. On-demand/manual trigger is a privileged operational action, not a replacement for the configured schedule. |
| `dailyRequestCap` | `500` | Maximum requests per tenant/date. |
| `allocationLookbackDays` | `10` | Used by `RecentAllocationCount` and default penalty expiry. |
| `lateCancellationPenalty` | `1` | Applies after a slot has been allocated. |
| `noShowPenalty` | `2` | Applies when no-show policy marks an allocation unused. |
| `manualAdjustmentEnabled` | `true` | Manual adjustments require reason and audit record. |
| `sameDayBookingEnabled` | `true` | Allows immediate allocation after Draw. |
| `sameDayUsesRequestCap` | `true` | Same-day requests count toward the same date cap. |
| `sameDayWaitlistEnabled` | `false` | Future feature. V1 rejects same-day requests when no suitable slot is available. |
| `automaticReallocationEnabled` | `true` | Released allocated slots are reassigned to the next eligible requestor. |
| `usageConfirmationRequired` | `false` | Tenant can enable confirmation when a reliable method exists. |
| `usageConfirmationWindowMinutes` | `0` | `0` means not enforced unless confirmation is enabled. |
| `usageConfirmationMethods` | Empty list | Enabled methods such as employee self-confirmation, HR manual confirmation, QR code, access-control import, or system import. |
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
- resource map version and zone preference behavior.

Location overrides must not silently remove required tenant-wide compliance, audit, or role-based access controls.

## Slot Capability Configuration

Each location must define its parking slots or capacity pools. In the broader FairSpot model, this is the parking-specific resource map.

A slot or capacity pool may define:

- slot ID or pool ID;
- location;
- zone or section;
- time-slot availability;
- supported vehicle types;
- EV charging availability;
- accessibility availability;
- motorcycle capacity;
- company-car-only restriction;
- reserved user or reserved group;
- active/inactive status.

FairSpot must not allocate a request to a slot that does not satisfy the request's configured constraints.

## Resource Map Upload

Customers should be able to upload or maintain a tenant-scoped resource map for each location.

For parking, the map represents spaces, sections, and capacity pools. Future resource modules can reuse the same concept for desks, chairs, seats, lockers, chargers, or other limited resources.

Rules:

- map publication requires validation before it affects allocation;
- maps must be versioned and auditable;
- each resource must have a stable ID or stable pool ID;
- resources may belong to a zone;
- zones may have employee-visible labels;
- resources may have capabilities and restrictions;
- inactive resources must not be allocated;
- historical allocations should retain the map/resource version used at allocation time.

## Zone Preference Policy

A tenant or location may configure how employee and team zone preferences affect allocation.

Suggested policy fields:

| Field | Default | Notes |
| --- | --- | --- |
| `zonePreferenceEnabled` | `false` | Enables employees to choose a preferred zone. |
| `teamDefaultZoneEnabled` | `false` | Enables team/department default zones. |
| `zoneFallbackEnabled` | `true` | Allows allocation outside preferred/default zone when compatible capacity exists. |
| `strictZonePreferenceAllowed` | `false` | Future option. When enabled and selected, an employee can choose to be rejected/waitlisted rather than assigned elsewhere. |
| `fallbackVisibleToEmployee` | `true` | Employee views should show that the assigned resource is outside the preferred zone. |

Rules:

- zone preference is soft by default;
- team default zones are soft by default;
- reserved zones require explicit reserved-space policy;
- fallback allocation must still satisfy hard resource constraints;
- audit records should capture requested zone, default team zone, assigned zone, and fallback reason where applicable.

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

- company-car requests use Tier 1 allocation only when the requestor has an HR/facilities-assigned compatible fixed company-car slot;
- Tier 1 allocation happens before Tier 2 lottery;
- Tier 1 allocations do not affect `RecentAllocationCount`;
- Tier 1 allocations do not create penalties;
- company-car entitlements may exceed fixed company-car slots, but that must be visible as a configuration warning because the guarantee cannot be honored for every company car;
- company-car employees without an active compatible fixed slot remain eligible for normal allocation when policy allows it, but their parking is not guaranteed.

The default warning should indicate configuration pressure: company-car entitlements exceed active compatible fixed company-car capacity.

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
- no-show detection requires at least one configured confirmation method;
- if no confirmation method is configured, unconfirmed usage remains unknown rather than no-show;
- confirmation source must be recorded;
- confirmation after the configured window requires an authorized manual correction;
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

- Given a tenant with no location override, when FairSpot evaluates a request, then it uses tenant default policy values.
- Given a location override, when FairSpot evaluates a request for that location, then overridden fields use the location value and all other fields fall back to tenant defaults.
- Given a request after the configured cut-off, when scheduled Draw submission is evaluated, then FairSpot rejects it as late.
- Given same-day booking is disabled, when an employee submits a same-day request, then FairSpot rejects it with a clear reason.
- Given usage confirmation is disabled, when an allocation is not confirmed, then FairSpot must not automatically mark it as no-show.
- Given no-show detection is enabled without a confirmation method, when the policy is published, then FairSpot rejects the policy as invalid.
- Given a policy change, when it is published, then FairSpot records an audit event with changed fields.
- Given a completed Draw, when policy changes later, then the completed Draw outcome remains unchanged.
