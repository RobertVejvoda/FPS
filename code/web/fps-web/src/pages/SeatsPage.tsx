import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { ModuleSwitch } from '../tenant/ModuleSwitch';
import { fetchBookings, submitBooking, type BookingListItem } from '../api/bookings';

// PLAT-seats (#710) — the employee's team-seat allocation surface. Deliberately NOT parking with
// renamed labels: it speaks in workplace/seat language (team area, seat, waitlist). The tenant app
// already uses demo facility/location constants for the showcase (see NewBookingPage), so seats
// follow the same pattern.
const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';
const DEFAULT_SEATS_LOCATION = 'GL-TEAMS';

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'ok'; seats: BookingListItem[] };

// Business-readable outcome copy — no raw ids beyond the human seat label (e.g. HQ-TEAM-A-03).
function seatOutcome(item: BookingListItem): { label: string; tone: 'ok' | 'wait' | 'off' } {
  switch (item.status) {
    case 'Allocated': return { label: `Seat reserved — ${item.allocatedSlotId ?? 'assigned'}`, tone: 'ok' };
    case 'Waitlisted':
    case 'Pending': return { label: 'On the seat waitlist', tone: 'wait' };
    case 'Cancelled': return { label: 'Cancelled', tone: 'off' };
    default: return { label: 'No seat available for this day', tone: 'off' };
  }
}

export function SeatsPage() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const cfg = useMemo(() => ({ apiBaseUrl, bearerToken }), [apiBaseUrl, bearerToken]);
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [date, setDate] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [flash, setFlash] = useState<string | null>(null);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    void fetchBookings(cfg).then((r) => {
      if (r.kind === 'ok') {
        setState({ kind: 'ok', seats: r.items.filter((i) => i.resourceType === 'Seats') });
      } else if (r.kind === 'unauthenticated') {
        setState({ kind: 'error', message: 'Your session is not authorized.' });
      } else if (r.kind === 'unreachable') {
        setState({ kind: 'error', message: 'Could not reach the booking service.' });
      } else {
        setState({ kind: 'error', message: r.message });
      }
    });
  }, [cfg]);

  useEffect(load, [load]);

  // Reuse the seats location the tenant already allocates at, if the employee has seat history;
  // otherwise fall back to the showcase seats location.
  const seatsLocation = useMemo(() => {
    if (state.kind === 'ok') {
      const known = state.seats.find((s) => s.locationId)?.locationId;
      if (known) return known;
    }
    return DEFAULT_SEATS_LOCATION;
  }, [state]);

  async function requestSeat() {
    if (!date) { setFlash('Pick a workday first.'); return; }
    setSubmitting(true);
    setFlash(null);
    const r = await submitBooking(cfg, {
      facilityId: DEMO_FACILITY_ID,
      locationId: seatsLocation,
      resourceType: 'Seats',
      // Vehicle fields are ignored for seats.
      licensePlate: 'N/A',
      vehicleType: 'Sedan',
      isElectric: false,
      requiresAccessibleSpot: false,
      isCompanyCar: false,
      plannedArrivalTime: `${date}T08:00:00`,
      plannedDepartureTime: `${date}T18:00:00`,
    });
    setSubmitting(false);
    if (r.kind === 'accepted') { setFlash(r.status === 'Allocated' ? 'Seat reserved.' : 'Seat request submitted — you’ll find out in the draw.'); setDate(''); load(); }
    else if (r.kind === 'rejected') setFlash(r.reason ?? 'Seat request could not be accepted.');
    else if (r.kind === 'unauthenticated') setFlash('Your session is not authorized.');
    else setFlash('message' in r ? r.message : 'Seat request failed.');
  }

  return (
    <section className="page-stack">
      <ModuleSwitch active="seats" />
      <header className="page-hero">
        <div>
          <h2>Team seats</h2>
          <p>Request a shared team seat for a workday. Seats are allocated by the same fair draw as parking, so when a day is popular a small waitlist forms.</p>
        </div>
      </header>

      <section className="plat-card">
        <h3>Request a seat</h3>
        <div className="seat-request-row">
          <label>Workday
            <input type="date" value={date} onChange={(e) => setDate(e.target.value)} aria-label="Seat date" />
          </label>
          <button className="btn-primary" disabled={submitting} onClick={() => { void requestSeat(); }}>
            {submitting ? 'Requesting…' : 'Request a seat'}
          </button>
        </div>
        {flash && <p className="plat-muted" role="status">{flash}</p>}
      </section>

      <section className="plat-card">
        <h3>Your seat requests</h3>
        {state.kind === 'loading' && <p className="plat-muted">Loading…</p>}
        {state.kind === 'error' && <p className="plat-error" role="alert">{state.message}</p>}
        {state.kind === 'ok' && state.seats.length === 0 && <p className="plat-muted">No seat requests yet. Request a seat above.</p>}
        {state.kind === 'ok' && state.seats.length > 0 && (
          <ul className="seat-list">
            {state.seats.map((s) => {
              const o = seatOutcome(s);
              return (
                <li key={s.requestId} className={`seat-card seat-${o.tone}`}>
                  <span className="seat-date">{s.requestedDate}</span>
                  <span className="seat-status">{o.label}</span>
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </section>
  );
}
