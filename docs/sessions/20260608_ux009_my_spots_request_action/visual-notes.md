# UX009: My Spots Request Action Visual Notes

## Summary

Moved My Spots request actions into day cards and removed duplicate date selection. Replaced `D+2`/`D+3` labels with named weekdays in HR Operations.

## Changes Made

### Employee My Spots (BookingsPage.tsx)

**Before:**
- Four day cards showing only existing bookings
- Separate "Request a spot" section below with duplicate date chips (Today/Tomorrow/D+2/D+3)
- No draw/cut-off timing on cards without requests

**After:**
- Four day cards now show:
  - Existing bookings with status, actions, and timing (unchanged)
  - **For days without requests:**
    - "No request yet" message
    - Next draw time (e.g., "Next draw: Tue 18:00")
    - Cut-off time (e.g., "Cut-off: Tue 18:00")
    - Demand level (e.g., "Demand: Medium")
    - Direct "Request a spot" button when requests are allowed
    - Business-readable blocked reason when requests aren't allowed (e.g., "Cannot request: The request cut-off has passed for today.")
- Day card labels use named weekdays: "Today", "Tomorrow", "Monday", "Tuesday" (not D+2/D+3)
- Removed duplicate "Request a spot" section with date chips for the four focus days
- Replaced with simplified "Request for other dates" section that links to full date picker for uncommon dates

### HR Operations (HrOperationsPage.tsx)

**Before:**
- Date chips labeled: Today, Tomorrow, D+2, D+3

**After:**
- Date chips labeled: Today, Tomorrow, [weekday name], [weekday name]
- Example: Today, Tomorrow, Monday, Tuesday

## Implementation Details

### BookingsPage.tsx Changes

1. **Enhanced FocusCard component:**
   - Added props: `onRequestForDate`, `drawStatus`, `drawLoading`
   - When no booking exists, shows draw/cut-off timing from API
   - Shows "Request a spot" button when `canRequest === true`
   - Shows business-readable blocked reason when `canRequest === false`

2. **Added per-day draw status fetching:**
   - New state: `drawStatusByDate`, `drawLoadingByDate`
   - Fetches draw status for each of the four focus days independently
   - Passes individual draw status to each card

3. **Removed duplicate request UI:**
   - Removed chip selector for Today/Tomorrow/D+2/D+3
   - Removed `selectedChip` state
   - Removed unused `CHIPS` constant and related variables
   - Replaced with "Request for other dates" section linking to full form

### HrOperationsPage.tsx Changes

1. **Added weekdayLabel helper:**
   - Converts offset to named weekday (e.g., "Monday", "Tuesday")

2. **Updated DATE_CHIPS:**
   - Changed from hardcoded "D+2", "D+3" to `weekdayLabel(2)`, `weekdayLabel(3)`

## Validation Results

### Typecheck
✅ `npm run typecheck` passes with no errors

### Grep Results
```bash
rg -n "D\+2|D\+3|Request a spot" code/web/fps-web/src/pages
```

**Results:**
- `code/web/fps-web/src/pages/BookingsPage.tsx:357` - "Request a spot" button text in FocusCard (intentional, per spec)
- `code/web/fps-web/src/pages/BookingsPage.tsx:365` - "Request a spot" fallback button text in FocusCard (intentional, per spec)
- `code/web/fps-web/src/pages/NewBookingPage.tsx:150` - "Request a spot" page title (intentional, correct context)

**Explanation:**
- No `D+2` or `D+3` labels found ✅
- All "Request a spot" occurrences are intentional:
  - Two in FocusCard component as button text when no booking exists (correct per spec)
  - One as the page title for the new booking form (correct, not employee-visible in the My Spots flow)

## Business-Readable Copy

All employee-facing text follows the spec:
- Uses "spot" not "slot" for employee context
- Shows business-readable timing: "Next draw: Tue 18:00", "Cut-off: Tue 18:00"
- Shows business reasons for blocked requests: "Cannot request: The request cut-off has passed for today."
- No technical errors, API routes, tenant IDs, or raw identifiers
- Day labels use "Today", "Tomorrow", and weekday names (Monday, Tuesday, etc.)

## User Experience Flow

### Employee without a request for a day:
1. See day card (e.g., "Wednesday")
2. Card shows "No request yet"
3. Card shows next draw time and cut-off
4. Card shows current demand level
5. If requests are open, see "Request a spot" button directly on card
6. Click button → navigate to request form pre-filled with that date

### Employee with an existing request:
- Card shows as before (status, actions, timing)
- For pending requests, also shows next draw and cut-off timing

### HR Operations:
- Date selector now shows business-friendly labels
- Behavior unchanged, only label improvement

## Files Modified

1. `code/web/fps-web/src/pages/BookingsPage.tsx`
2. `code/web/fps-web/src/pages/HrOperationsPage.tsx`

## Files Created

1. `docs/sessions/20260608_ux009_my_spots_request_action/visual-notes.md` (this file)
