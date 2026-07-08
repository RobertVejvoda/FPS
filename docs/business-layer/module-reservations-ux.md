# Module Reservations UX

This page records the product and UX direction for moving FairSpot from a parking-led proof path to a module-aware scarce-capacity experience. It is the source document for future implementation slices that introduce Seats and improve the employee web/mobile flow.

Parking remains the first implemented module. Seats must not become a duplicated parallel app. Parking, Seats, and later modules should reuse the same reservation, request, Draw, notification, audit, reporting, and profile foundations while exposing only module-relevant choices and explanations to the user.

## UX Principles

- The employee experience is **module-aware**, not module-fragmented.
- Tenant module configuration controls which modules appear. User eligibility affects warnings, validation, defaults, and allocation, but it does not hide the module from the menu.
- The primary employee surfaces are one combined **My Reservations** page and one unified **Request** page.
- Use quick date and time selection first. A date picker may exist only as a secondary escape hatch for uncommon dates.
- Show module-specific details only after they are relevant. Do not duplicate full Parking and Seats menu trees.
- Use business language: reservation, request, seat, area, team, spot, allocation, waiting, preference. Do not expose tenant IDs, raw user IDs, GUIDs, API names, draw seeds, candidate order, or hidden weighting details.
- UX implementation must improve clarity and reduce current parking/seat mixing before broadening module functionality.

## Employee Information Architecture

Recommended employee menu:

```text
-- My Reservations
|   +-- Date-grouped reservations and requests across enabled modules
|   +-- Module badges and filters
|   +-- Detail drawer/page for each reservation or request
+-- Request
|   +-- Date/time first
|   +-- Enabled module options for that date/time
|   +-- One submit action for one or more selected modules
+-- My Profile
|   +-- Vehicles and parking facts
|   +-- Seat preferences and recurrence
|   +-- Notification preferences
+-- Notifications
+-- About / Legal
```

If only Parking is enabled, the UI should feel like a simple parking experience without exposing empty Seats concepts. If Parking and Seats are enabled, the user should still see one reservations list and one request entry point.

## My Reservations

The combined **My Reservations** page shows all user-facing reservation records for enabled modules.

Default grouping is by date, because users usually ask "what do I have today/tomorrow/next week?" before they think by module.

```text
My Reservations

Today
  Parking   Allocated       Spot 349 - Zone A        08:00-18:00
  Seat      Waiting         Team Area North           08:00-18:00

Tomorrow
  Parking   Waiting         Next draw today 18:00     08:00-18:00
  Seat      Waitlisted      More requests than seats  08:00-12:00

Friday
  Seat      Cancelled       Cancelled by you          12:00-18:00
```

Rules:

- show pending requests, allocated reservations, waitlisted requests, cancelled items, and historical used/no-show states when the module has usage confirmation enabled;
- group by date first, then show module rows inside the date group;
- use module badges and optional filters, not separate duplicated module pages;
- rows open module-aware details with the correct resource facts and safe allocation explanation;
- cancelled and historical records may be collapsed or filterable when the list grows, but they remain accessible.

## Unified Request Page

The unified **Request** page starts with date/time, then shows module choices for that selected time.

```text
Request

[Today] [Tomorrow] [Wednesday] [Thursday] [More dates]

Time
[08:00-18:00] [08:00-12:00] [12:00-18:00]

For Wednesday 08:00-18:00
[ ] Parking
    Vehicle: Sedan EV
    Preferred area: Zone A

[ ] Seat
    Preferred seat: Team A-04
    Alternate seats: Team A-05, Team A-06
    Fallback: Any allowed team-area seat

[Submit selected requests]
```

Rules:

- date comes first;
- time choices mirror Parking and display actual hours: whole day, morning, afternoon;
- module options are shown only for tenant-enabled modules;
- one action can submit multiple independent module requests for the selected date/time;
- partial success is allowed by default: a valid Seat request may be created even if Parking fails, and the reverse is also valid;
- each module result must be reported clearly after submit;
- linked all-or-nothing requests are an advanced tenant-configured feature and default off.

## Linked Requests

Parking and Seats are independent by default. A user may request both for the same date/time, and each module runs its own allocation.

Some tenants may want a linked mode, for example "only allocate my Seat if Parking is also allocated." This is allowed as an advanced capability, but it must be tenant-configured and default off because it complicates fairness and capacity.

Rules:

- linked requests are strict all-or-nothing;
- all linked module requests are allocated, or none are allocated;
- the UI must explain that linking can reduce allocation chances;
- linked allocation requires cross-module coordination and should be implemented after the independent module flow is stable.

## Seats Module Direction

Seats is a tenant-enabled FairSpot module that reuses the same scarce-capacity reservation model as Parking.

Employee behavior:

- users can request any seat;
- users can choose a specific preferred seat within their dedicated/allowed area;
- users can configure alternate specific seats in their profile;
- preferences are inputs to allocation, not guarantees;
- if preferred seats are unavailable, fallback applies in configured order and may end at any allowed seat.

Profile and administration:

- team membership comes from Profile/HR roster facts;
- seat preferences are self-service by default;
- HR/Admin can override user seat preferences, with audit evidence;
- HR/Admin configures physical areas/seats and can block seats for a date or date range;
- blocked seats are excluded from allocation and visible on the seat map.

Time model:

- default is whole day;
- tenants may also allow morning and afternoon;
- all options are displayed as actual hours, not abstract labels.

Usage confirmation and penalties:

- Seats starts with usage confirmation and no-show penalties off by default;
- tenants may enable them later;
- when enabled, the module should reuse the same confirmation/penalty pattern as Parking where practical.

Cancellation and waitlist:

- users can cancel seat reservations;
- cancelled seats return to available capacity;
- reallocation pulls from valid waitlisted requests using the same Seats fairness and team-priority rules;
- waitlist exists only when valid demand exceeds available capacity.

## Seats Allocation

Seats uses fair allocation. It is not first-come-first-served.

Seat areas belong to teams. Allocation runs with team-area priority:

1. For a team-owned area, first allocate eligible requests from users who belong to that team.
2. If capacity remains, allocate remaining seats to eligible requests from other teams.
3. No reserved percentage is needed; the rule is owning team first, leftovers open to others.

Specific-seat and alternate-seat preferences participate inside this model:

- an owning-team user with a preferred seat in that team area is considered before outside-team requests for that area;
- an outside-team user's preference for that area can be considered only after owning-team demand has had first priority;
- if a preferred seat is unavailable, allocation tries configured alternates, then fallback where allowed;
- audit records must capture requested preference, assigned seat, fallback, and team-priority reason where relevant.

## Seat Map

Seats should include a map from the start, but the first implementation can be a simple grid rather than a detailed office floorplan.

Minimum model:

- area;
- row;
- column;
- stable seat ID;
- employee-safe label;
- owning team;
- active/inactive state;
- date/time availability;
- block state and reason category;
- basic capabilities where needed, such as monitor setup or accessibility.

The map should support selection of a preferred seat and alternate seats. It should clearly distinguish unavailable, blocked, allocated, team-priority, and selectable seats without exposing other users.

## Recurring Requests

Seats should support recurring request preferences, same as the parking-slot request direction.

Rules:

- users save a recurrence rule and preference order;
- the system creates concrete requests shortly before the Draw window, not immediately for the full future horizon;
- changes to preferences or recurrence apply only to future not-yet-generated requests;
- already generated requests and reservations remain unchanged unless the user cancels or replaces them;
- latest team membership, seat blocks, and preference state should be evaluated close to generation/allocation time.

## UX Improvement Backlog

Before Seats is implemented broadly, the current UX needs a cleanup pass:

- replace duplicated parking-specific navigation with the combined **My Reservations** and **Request** model;
- keep Parking as a module label/badge where useful, not the page structure;
- ensure Seats demo content does not appear inside parking-only views;
- make date/time quick selection reusable across modules;
- add module filters and badges to reservation lists;
- add employee-safe empty, partial-success, and validation states;
- keep web and mobile information hierarchy aligned.

## Open Implementation Questions

The product direction above is agreed enough to document, but implementation still needs slicing decisions:

- whether the first Seats slice should build backend contracts first or UX shell first;
- how much of linked all-or-nothing allocation belongs in v1 versus later;
- whether "any allowed seat" fallback is always available or tenant-configurable;
- exact profile API shape for self-service seat preferences and HR/Admin override evidence;
- exact event names and DataHub projection shape for cross-module reservations.
