# Allocation Demand Transparency

## Purpose

This document defines how FPS exposes allocation demand and capacity context to employees while preserving privacy, preventing gaming incentives, and maintaining trust in the fairness promise.

## Product Decision

Employee-facing demand transparency uses **coarse demand levels** (Low/Medium/High) rather than exact request/spot counts. This framing:

- increases trust by showing rejection is due to real scarcity, not hidden decisions;
- reduces uncertainty while requests are pending;
- helps employees plan alternative commutes;
- supports rejection explanations with visible demand context;
- avoids misleading precision from raw counts that ignore eligibility, reserved slots, and fairness rules;
- prevents gaming behavior around exact thresholds;
- protects small groups and sensitive categories from privacy leakage.

## Scope

### In Scope

- Employee-facing **allocation outlook** for selected date/location/time slot
- Coarse demand level labels: `Low`, `Medium`, `High`
- Next draw timing and explanation that allocation follows policy/fairness rules
- Privacy threshold rules to hide exact counts for small cohorts
- Mobile pending booking card/detail integration
- Future web booking status integration

### Out Of Scope (Phase 1)

- Raw operational diagnostics (exact request counts, slot counts by type, draw seed/order, penalty weights, hidden prioritization details)
- Employee allocation history visibility (recent allocation count, penalty score, tier-2 weight)
- Waitlist position or ranking
- Real-time capacity utilization by slot type (EV charging, accessible, company-car)
- Demand prediction, peak demand trends, or rejection rate analytics
- Admin/reporting detailed operational views (may be added later with different authorization)

## Business Rules

### Demand Level Calculation

For a given draw key (tenant, location, date, time slot):

- **Low demand**: Pending request count ≤ 50% of available slot count
- **Medium demand**: Pending request count > 50% and ≤ 100% of available slot count
- **High demand**: Pending request count > 100% of available slot count

When computing available slot count:

- Include all slots configured for the location and time slot
- Do **not** subtract reserved slots (company-car, accessible) unless employee UI explicitly shows slot-type breakdowns (future enhancement)
- Treat same-day already-allocated slots as unavailable for demand calculation

### Privacy Threshold Rules

Exact counts or fine-grained demand levels must not be shown when:

- Total pending request count < 10 (small cohort rule)
- Total available slot count < 5 (small capacity rule)
- Location or time slot is designated as privacy-sensitive in tenant policy (future enhancement)

When privacy threshold is triggered, show:

- `Demand: Limited spaces available`
- `Demand details hidden because the group is too small.`

### Timestamp Transparency

Demand context responses must include:

- `updatedAt`: timestamp when demand calculation was performed
- Explanation that counts may change before draw runs

Demand context is **snapshot data**, not real-time. Stale numbers can create disputes, so UI must make timing clear.

## API Contract

### Demand Context Response

`GET /bookings/{requestId}` response should include optional `demandContext` field when safe and available:

```json
{
  "data": {
    "bookingRequestId": "...",
    "status": "Pending",
    "requestedDate": "2026-05-27",
    "timeSlot": {
      "start": "08:00",
      "end": "18:00"
    },
    "locationId": "building-a",
    "demandContext": {
      "demandLevel": "High",
      "nextDrawTime": "2026-05-26T18:00:00Z",
      "updatedAt": "2026-05-25T12:34:56Z",
      "explanation": "Final allocation depends on eligibility and fairness rules."
    }
  }
}
```

`GET /bookings` list response should **not** include `demandContext` in list items for performance and complexity reasons. Demand context is detail-view only.

#### DemandContext Schema

| Field | Type | Description |
| --- | --- | --- |
| `demandLevel` | `"Low"` \| `"Medium"` \| `"High"` \| `"Limited"` | Coarse demand signal. `Limited` means privacy threshold triggered. |
| `nextDrawTime` | ISO 8601 timestamp | When the scheduled draw will run for this request. |
| `updatedAt` | ISO 8601 timestamp | When this demand snapshot was calculated. |
| `explanation` | string | Short employee-safe explanation. |

When privacy threshold is triggered, use `demandLevel: "Limited"` and set `explanation` to `"Demand details hidden because the group is too small."`.

### Authorization

- `GET /bookings/{requestId}` with `demandContext` requires authenticated employee actor and ownership validation (actor must own the booking request or have admin/HR role).
- Demand context must not expose other employees' identities, protected category counts, or internal allocation weights.

## Mobile UI Integration

### Pending Booking Card

Display demand context on booking cards when status is `Pending`:

- Show demand level badge with appropriate color:
  - Low: Green or muted color
  - Medium: Amber/yellow
  - High: Orange or warning color
  - Limited: Neutral/muted color
- Show next draw time: `Next draw: Tue 26 May, 18:00`
- Include info icon with explanation popover or inline text

Example layout:

```
┌─────────────────────────────────────┐
│ Wed 27 May                [Pending] │
│ 08:00 - 18:00                       │
│ Building A                          │
│                                     │
│ Demand: High 🔶                     │
│ Next draw: Tue 26 May, 18:00       │
│                                     │
│ [Cancel Request]                    │
└─────────────────────────────────────┘
```

### Booking Detail View

Display full demand context in detail view:

- Demand level with color-coded label
- Next draw time
- Last updated timestamp
- Explanation text (inline or expandable section)

Example detail section:

```
Demand Outlook
--------------
Demand: High
Next draw: Tue 26 May, 18:00
Updated: 25 May, 12:34

Final allocation depends on eligibility
and fairness rules. High demand means
requests exceed available capacity.
```

### Loading and Error States

- While demand context is loading, show placeholder or omit section
- If demand context cannot be calculated (e.g., draw already completed, same-day allocation, or API error), omit section gracefully
- Do not show demand context for `Allocated`, `Rejected`, `Cancelled`, `Used`, `NoShow`, or `Expired` statuses (demand context is only relevant for `Pending` status)

## Web UI Integration (Future)

Web booking status page should follow similar patterns:

- Show demand context on pending booking cards/detail
- Use consistent color-coded badges and explanation text
- Provide "What does this mean?" expandable sections for each demand level

## Implementation Guidance

### Backend Implementation

1. Add `DemandContextService` application service with method:
   - `GetDemandContextForBookingAsync(BookingRequest booking)` → `DemandContext?`
2. Implement demand level calculation using booking query repository:
   - Count pending requests for draw key
   - Count available slots from slot service
   - Apply privacy threshold rules
   - Compute demand level enum
3. Integrate into `GetMyBookingsQueryHandler` detail projection:
   - Call demand context service when status is `Pending`
   - Include `demandContext` in response DTO
4. Add unit tests for demand level calculation edge cases
5. Add integration tests for privacy threshold rules

### Frontend Implementation

1. Update `BookingListItem` TypeScript type to include optional `demandContext`
2. Update `BookingCard.tsx` component to display demand context when present and status is `Pending`
3. Add demand level badge with color styling
4. Add info icon with explanation popover
5. Update `booking/[requestId].tsx` detail page to show full demand context section
6. Handle loading/error states gracefully

### Configuration

Demand calculation parameters should be configurable per tenant (future enhancement):

- Privacy threshold minimum request count (default: 10)
- Privacy threshold minimum slot count (default: 5)
- Low demand percentage threshold (default: 50%)
- High demand percentage threshold (default: 100%)

## Validation Acceptance Criteria

- [ ] Pending booking detail API response includes `demandContext` when safe and available
- [ ] Demand level is `Low`/`Medium`/`High` based on pending request vs available slot ratio
- [ ] Demand level is `Limited` when privacy threshold (< 10 requests or < 5 slots) is triggered
- [ ] Explanation text clarifies that counts are informational and allocation follows policy/fairness rules
- [ ] Exact counts, technical IDs, tenant wording, raw policy IDs, draw internals, and user identities are not exposed
- [ ] Mobile booking card shows demand level badge and next draw time for pending requests
- [ ] Mobile booking detail shows full demand context section with timestamp
- [ ] Demand context is omitted for non-pending statuses (`Allocated`, `Rejected`, etc.)
- [ ] Unit tests cover demand level calculation edge cases
- [ ] Integration tests verify privacy threshold rules

## Future Enhancements

Possible future additions (out of scope for Phase 1):

- Admin/reporting view with detailed operational diagnostics (exact counts, draw results, waitlist stats)
- Historical demand trends and peak demand time recommendations
- Employee allocation history visibility (recent count, penalty score) for transparency
- Slot-type breakdown (EV charging, accessible, company-car reserved) with separate demand levels
- Configurable demand calculation parameters per tenant
- Real-time demand updates via websocket or polling (currently snapshot-based)
- Demand prediction and recommended alternative dates

## Related Documentation

- [Booking API Contract](./booking-api-contract)
- [Booking Request Lifecycle](./booking-request-lifecycle)
- [Allocation Rules](./allocation-rules)
- [Booking Authorization](./booking-authorization)
- [Booking Reason Codes](./booking-reason-codes)
- [Booking Implementation Slices](../implementation/booking-vertical-slices)
