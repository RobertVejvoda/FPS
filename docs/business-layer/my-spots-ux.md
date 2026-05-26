# My Spots Employee UX

This page defines the employee-facing My Spots experience for web and mobile. It is the source-of-truth UX spec for `UX007`.

The design goal is simple:

> Do I have a spot when I need one, and if not, what can I do?

FairSpot should feel like a practical daily tool, not an administration console. The employee should immediately see today, tomorrow, whether they can request a spot, what happened to recent requests, and why an allocation result happened.

## Product Language

Use business language on employee screens.

| Use | Avoid on employee screens |
| --- | --- |
| Company | Tenant |
| Spot | Slot when referring to an employee's place to use |
| Request | Booking when the user action is asking for a spot |
| Allocation | Draw internals, lottery seed, weights |
| Area or zone | Raw location IDs |
| Vehicle | Vehicle GUID or technical profile ID |

Never show raw user IDs, tenant IDs, GUIDs, API URLs, object-storage paths, or technical claims on employee screens. Admin and operations documentation may still use tenant terminology where it is technically precise.

The main employee area is **My Spots**. It is intentionally generic so the same product model can later support parking, seats, desks, lockers, chargers, or other limited workplace resources.

## Information Architecture

```text
Employee
+-- My Spots
|   +-- Today / tomorrow focus
|   +-- Quick request
|   +-- My requests table
|   +-- Request detail
+-- My Profile
|   +-- Preferences
|   +-- My Vehicles
+-- Notifications
+-- About / Legal

HR / Admin additions
+-- Company setup
+-- People / profiles
+-- Locations and spots
+-- Policies
+-- Reports
+-- Audit
```

`My Spots` should be the default employee landing page after login. Role-specific admin, HR, reporting, and audit pages should only appear for users who can use them.

## Default My Spots Screen

The default page is one practical view with three parts:

1. Today and tomorrow focus.
2. Quick request controls.
3. A compact request table showing today, upcoming, and a few past records.

```text
[My Spots]
+----------------------------------------+
| My Spots                               |
| Alice Novak                            |
| Demo Company                           |
+----------------------------------------+
| Today                                  |
| Allocated                              |
| Spot 349 - Zone A                      |
| 08:00-17:00 - EV charger               |
| [Details] [Cancel]                     |
+----------------------------------------+
| Tomorrow                               |
| Waiting for draw                       |
| Next draw: Today 18:00                 |
| Demand so far: Medium                  |
| [Details] [Change]                     |
+----------------------------------------+
| Request a spot                         |
| [Today] [Tomorrow] [D+2] [D+3] [More]  |
| Demand: Medium                         |
| Can request: Yes                       |
+----------------------------------------+
| My requests                            |
| Showing 8 of 42 records                |
|                                        |
| Date        State       Result         |
| Today       Allocated   Spot 349       |
| Tomorrow    Waiting     Draw 18:00     |
| Thu 28 May  Pending     -              |
| Fri 29 May  Not alloc.  No spot        |
| Mon 25 May  Used        Spot 122       |
| Fri 22 May  Not alloc.  Not selected   |
| Thu 21 May  Cancelled   By you         |
| Wed 20 May  Used        Spot 410       |
|                                        |
| [Load more]                            |
+----------------------------------------+
```

### Default Ordering

The request table should show a useful mixed window, not only future records:

1. Today.
2. Tomorrow.
3. Future/upcoming requests.
4. A few recent past records.

The table must show total count, for example `Showing 8 of 42 records`.

Rows are clickable. Past rows open read-only detail. Future or active rows open detail with allowed actions.

## Request Detail

Request detail is where FairSpot explains the result.

Past requests are read-only. Current and future requests may allow change, cancel, confirm usage, or other context-specific actions.

```text
[Request Detail]
+----------------------------------------+
| Tomorrow                               |
| Waiting for draw                       |
+----------------------------------------+
| Your request                           |
| Vehicle: Sedan - EV                    |
| Preferred area: Zone A                 |
| Submitted: Today 09:14                 |
| [Change] [Cancel]                      |
+----------------------------------------+
| Allocation explanation                 |
| Next draw: Today 18:00                 |
| Demand so far: Medium                  |
| Requests so far: 31                    |
| Available spots: 24                    |
| You are eligible                       |
+----------------------------------------+
| Timeline                               |
| [x] Request submitted                  |
| [ ] Draw scheduled                     |
| [ ] Result notification                |
+----------------------------------------+
```

After the draw completes:

```text
Allocation explanation
Draw completed: Today 18:02
Demand: High
Requests: 38
Available spots: 24
Result: Not allocated
Why: More eligible requests than available spots. The draw followed company policy.
```

Allocated result:

```text
Allocation explanation
Draw completed: Today 18:02
Demand: Medium
Requests: 21
Available spots: 24
Result: Allocated
Why: Your request matched an available EV-capable spot.
```

Do not expose lottery weights, seeds, ordered candidate lists, other employees, hidden slot metadata, or policy internals that would be private or gameable. Those belong in auditor/admin Draw lifecycle evidence, not the employee view.

## Availability Summary

The employee UI needs an availability summary, but should not calculate it locally.

Source of truth:

- Configuration owns configured active spots/capacity, location policy overrides, date/time availability, capability constraints, closures, and manual blocks.
- Booking owns current requests, allocations, waiting state, draw state, and employee-facing request outcomes.
- Booking should expose an employee-safe summary assembled from the effective configuration plus booking state.

Recommended API shape:

```text
GET /draws/status?locationId=LOC-MAIN&date=2026-05-27&timeSlot=workday
```

Employee-safe response shape:

```json
{
  "date": "2026-05-27",
  "nextDrawAt": "2026-05-26T18:00:00+02:00",
  "drawState": "Scheduled",
  "demandLevel": "Medium",
  "requestCount": 31,
  "availableSpotCount": 24,
  "canRequest": true,
  "cannotRequestReason": null
}
```

For the demo, exact `requestCount` and `availableSpotCount` are useful. Production profiles may configure visibility:

| Visibility | Example |
| --- | --- |
| Exact | `24 spots available` |
| Coarse | `Limited availability` |
| Hidden | `Demand: High` only |

The draw and the UI must use the same effective capacity logic so the employee explanation does not drift from allocation behavior.

## Quick Request

Most employee requests should not require a calendar. The quick request area should cover the common case:

```text
Request a spot
[Today] [Tomorrow] [D+2] [D+3] [More]
Demand: Medium
Can request: Yes
```

Rules:

- **Today**, **Tomorrow**, **D+2**, and **D+3** are large, touch-friendly date chips.
- **More** opens a calendar/date-time picker for uncommon dates.
- The selected date updates demand, availability, next draw, and can-request state.
- If the employee cannot request the selected date, show a business reason, not an HTTP/API error.

Examples:

- `Can request: No. The request cut-off has passed for today.`
- `Can request: No. Your profile needs an active vehicle.`
- `Can request: No. This company location has no active spots for that day.`

## My Vehicles And Preferences

Vehicles should not be edited inside the request form. They belong under profile.

```text
My Profile
+-- Preferences
|   +-- Preferred area / zone
|   +-- Preferred capabilities
|   +-- Future resource preferences
+-- My Vehicles
    +-- Add vehicle
    +-- Edit vehicle
    +-- Deactivate vehicle
```

Request forms should default from profile:

- active personal vehicle;
- company car where applicable;
- EV/accessibility/capability flags;
- preferred area or zone where policy supports it.

Cards should avoid full license plates unless the tenant policy requires them. Prefer vehicle type/model, capability flags, company-car flag, or masked plate.

## Web Layout

Web should use the same information hierarchy, with more horizontal space:

```text
+------------------------------+------------------------------+
| Today                        | Tomorrow                     |
| Allocated                    | Waiting for draw             |
| Spot 349 - Zone A            | Next draw: Today 18:00       |
| [Details] [Cancel]           | [Details] [Change]           |
+------------------------------+------------------------------+
| Request a spot                                                |
| [Today] [Tomorrow] [D+2] [D+3] [More]  Demand: Medium         |
+---------------------------------------------------------------+
| My requests                                      Showing 8/42 |
| Date        State       Result       Vehicle     Action       |
| Today       Allocated   Spot 349     Sedan EV    Details      |
| Tomorrow    Waiting     Draw 18:00   Sedan EV    Change       |
+---------------------------------------------------------------+
```

Use a detail page or side drawer for request detail. A side drawer is acceptable on desktop if it does not hide critical table content and remains accessible.

## Mobile Layout

Mobile should be one column and card-first:

```text
My Spots
+-- Today card
+-- Tomorrow card
+-- Request date chips
+-- Demand/can-request row
+-- My requests compact rows
```

Rows must remain touch-friendly. Text must not wrap into broken or overlapping layouts on small screens. Avoid dense admin-style tables on mobile; use compact row cards with date, state, result, and one action affordance.

## Visual Direction

The screen should be calm and operational:

- use FairSpot green for positive allocation and request-possible states;
- use amber for waiting/pending;
- use red only for blocked or needs-attention states;
- use neutral surfaces for historical rows;
- use compact status chips and icons where available;
- keep cards to modest radius and avoid nested cards;
- avoid showing technical labels in empty/error states.

Suggested employee-facing states:

| State | Tone | Copy example |
| --- | --- | --- |
| Allocated | Positive | `Spot 349 - Zone A` |
| Waiting for draw | Neutral/amber | `Next draw: Today 18:00` |
| Waiting for released spot | Neutral/amber | `Eligible, waiting for a released spot` |
| Not allocated | Neutral/red only if action needed | `More eligible requests than available spots` |
| Needs attention | Red/amber | `Add an active vehicle to request a spot` |
| Used | Neutral/positive | `Used - Spot 122` |
| Cancelled | Neutral | `Cancelled by you` |

## Acceptance Criteria

- Employee landing page is `My Spots` for employee-only users.
- Employee screens do not show raw user ID, tenant ID, tenant wording, GUIDs, API URLs, or technical storage identifiers.
- `Tenant` is replaced by `Company` or hidden on employee-facing screens.
- `My Spots` shows today/tomorrow allocation focus, quick request controls, demand/can-request state, and a mixed request table with total count.
- Quick request supports Today, Tomorrow, D+2, D+3, and a secondary date picker.
- Request rows are clickable.
- Past request details are read-only.
- Current/future request details expose only valid actions.
- Request detail shows allocation explanation, next/completed draw time, demand level, request count, available spot count when permitted, outcome, reason, and timeline.
- Availability summary comes from Booking using effective Configuration capacity; the client does not calculate total available spots locally.
- Vehicle management is reachable as `My Vehicles` under profile, not embedded as primary request-form editing.
- Web and mobile use the same information hierarchy with layout adapted to screen size.

## Implementation Notes For Claude

Expected implementation is likely split if the change is too large:

1. Employee terminology and navigation cleanup.
2. My Spots default page layout on web and mobile.
3. Request detail allocation explanation.
4. My Vehicles/profile navigation cleanup.

Preserve backend scoping rules: clients never send tenant, user, or role identifiers for employee API scoping. Use existing authenticated APIs where possible. If a field is missing from existing APIs, add an employee-safe backend field rather than leaking internal IDs or deriving values client-side.
