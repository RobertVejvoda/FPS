import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { fetchHrBookings, type HrBookingListItem } from '../api/bookings';

// PLAT-seats (#710) — HR/facilities seat view. Shows the seat capacity story for a chosen day:
// how many seats were requested, how many were allocated, and how many are waitlisted — plus which
// named seats were taken. No raw employee ids (AC): this view is about demand vs capacity, not who.
const DEFAULT_SEATS_LOCATION = 'GL-TEAMS';

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'forbidden' }
  | { kind: 'ok'; rows: HrBookingListItem[] };

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

export function SeatOperationsPage() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const cfg = useMemo(() => ({ apiBaseUrl, bearerToken }), [apiBaseUrl, bearerToken]);
  const [date, setDate] = useState(today());
  const [location, setLocation] = useState(DEFAULT_SEATS_LOCATION);
  const [state, setState] = useState<State>({ kind: 'loading' });

  const load = useCallback(() => {
    if (!date) return;
    setState({ kind: 'loading' });
    void fetchHrBookings(cfg, { locationId: location.trim() || DEFAULT_SEATS_LOCATION, from: date, to: date }).then((r) => {
      if (r.kind === 'ok') setState({ kind: 'ok', rows: r.items.filter((i) => i.resourceType === 'Seats') });
      else if (r.kind === 'unauthenticated') setState({ kind: 'error', message: 'Your session is not authorized.' });
      else if (r.kind === 'forbidden') setState({ kind: 'forbidden' });
      else if (r.kind === 'unreachable') setState({ kind: 'error', message: 'Could not reach the booking service.' });
      else setState({ kind: 'error', message: r.message });
    });
  }, [cfg, date, location]);

  useEffect(load, [load]);

  const summary = useMemo(() => {
    if (state.kind !== 'ok') return { requested: 0, allocated: 0, waitlisted: 0, seats: [] as string[] };
    const rows = state.rows;
    const allocated = rows.filter((r) => r.status === 'Allocated');
    const waitlisted = rows.filter((r) => r.status === 'Pending' || r.status === 'Waitlisted');
    return {
      requested: rows.length,
      allocated: allocated.length,
      waitlisted: waitlisted.length,
      seats: allocated.map((r) => r.allocatedSlotId ?? '—').sort(),
    };
  }, [state]);

  return (
    <section className="plat-page">
      <header className="plat-page-head">
        <h1>Seat requests</h1>
        <p className="plat-muted">Team-seat demand and capacity for a chosen day. Seats are allocated by the same fair draw as parking; when demand is higher than capacity, the extra requests form a waitlist.</p>
      </header>

      <div className="plat-filters">
        <label>Day
          <input className="plat-input" type="date" value={date} onChange={(e) => setDate(e.target.value)} aria-label="Seat day" />
        </label>
        <label>Seat area
          <input className="plat-input" value={location} onChange={(e) => setLocation(e.target.value)} aria-label="Seat area" />
        </label>
      </div>

      {state.kind === 'loading' && <p className="plat-muted">Loading…</p>}
      {state.kind === 'forbidden' && <p className="plat-error" role="alert">You don’t have access to seat operations.</p>}
      {state.kind === 'error' && <p className="plat-error" role="alert">{state.message}</p>}

      {state.kind === 'ok' && (
        <>
          <div className="plat-card-grid">
            <div className="plat-card"><h3>Requested</h3><p className="seat-metric">{summary.requested}</p><p className="plat-muted">seat requests for this day</p></div>
            <div className="plat-card"><h3>Allocated</h3><p className="seat-metric">{summary.allocated}</p><p className="plat-muted">seats filled</p></div>
            <div className="plat-card"><h3>Waitlisted</h3><p className="seat-metric">{summary.waitlisted}</p><p className="plat-muted">waiting if a seat frees up</p></div>
          </div>

          <div className="plat-card">
            <h3>Seats allocated</h3>
            {summary.seats.length === 0
              ? <p className="plat-muted">No seats allocated for this day yet.</p>
              : <ul className="seat-chip-row">{summary.seats.map((s, i) => <li key={i} className="seat-chip">{s}</li>)}</ul>}
          </div>
        </>
      )}
    </section>
  );
}
