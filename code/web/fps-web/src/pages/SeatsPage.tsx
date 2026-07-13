import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, submitBooking } from '../api/bookings';
import { isWorkday, nextWorkdayOptions, toLocalDateString } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';

// PLAT-seats (#710) — the employee's team-seat request surface. Deliberately NOT parking with
// renamed labels: it speaks in workplace/seat language (team area, seat, waitlist). The tenant app
// already uses demo facility/location constants for the showcase (see NewBookingPage), so seats
// follow the same pattern.
// UX008 (#781) — this page is request-only and date-first: the same quick workday selection as
// the Parking request flow, with a date input only as the secondary escape hatch. Seat
// reservations, waitlist state, and history live on the combined My Reservations page.
const DEMO_FACILITY_ID = '00000000-0000-0000-0000-000000000001';
const DEFAULT_SEATS_LOCATION = 'GL-TEAMS';

export function SeatsPage() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const navigate = useNavigate();
  const cfg = useMemo(() => ({ apiBaseUrl, bearerToken }), [apiBaseUrl, bearerToken]);
  const { dateBase, simulationActive } = useTenantDateContext();
  const quickDates = useMemo(() => nextWorkdayOptions(dateBase, 5, { relativeLabels: !simulationActive }), [dateBase, simulationActive]);
  const [seatsLocation, setSeatsLocation] = useState(DEFAULT_SEATS_LOCATION);
  const [date, setDate] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [flash, setFlash] = useState<string | null>(null);

  // Same default-date rule as the parking request form: tomorrow on a workday base,
  // otherwise the next workday.
  useEffect(() => {
    if (date) return;
    const defaultDate = (isWorkday(dateBase) ? quickDates[1] : quickDates[0])?.date;
    if (defaultDate) setDate(defaultDate);
  }, [date, dateBase, quickDates]);

  // Reuse the seats location the tenant already allocates at, if the employee has seat history;
  // otherwise fall back to the showcase seats location.
  const resolveLocation = useCallback(() => {
    void fetchBookings(cfg).then((r) => {
      if (r.kind !== 'ok') return;
      const known = r.items.find((i) => i.resourceType === 'Seats' && i.locationId)?.locationId;
      if (known) setSeatsLocation(known);
    });
  }, [cfg]);

  useEffect(resolveLocation, [resolveLocation]);

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
    if (r.kind === 'accepted') { setFlash(r.status === 'Allocated' ? 'Seat reserved. You can follow it under My Reservations.' : 'Seat request submitted — you’ll find out in the draw. Follow it under My Reservations.'); }
    else if (r.kind === 'rejected') setFlash(r.reason ?? 'Seat request could not be accepted.');
    else if (r.kind === 'unauthenticated') setFlash('Your session is not authorized.');
    else setFlash('message' in r ? r.message : 'Seat request failed.');
  }

  return (
    <section className="page-stack">
      <header className="page-hero">
        <div>
          {/* The page hero is brand-green; the back link must stay readable on it. */}
          <button
            onClick={() => navigate('/bookings')}
            style={{ background: 'none', border: 'none', color: 'rgba(255,255,255,0.92)', cursor: 'pointer', fontSize: 14, padding: 0, marginBottom: 8 }}
          >
            ← My Reservations
          </button>
          <h2>Request a seat</h2>
          <p>Request a shared team seat for a workday. Seats are allocated by the same fair draw as parking, so when a day is popular a small waitlist forms. Your seat requests and reservations appear on My Reservations.</p>
        </div>
      </header>

      <section className="plat-card">
        <h3>Request a seat</h3>
        {/* Date first — quick workday choices, same options as the parking request flow. */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, margin: '10px 0' }}>
          {quickDates.map((option) => {
            const selected = date === option.date;
            return (
              <button
                key={option.date}
                type="button"
                onClick={() => setDate(option.date)}
                style={{
                  padding: '6px 12px',
                  borderRadius: 6,
                  border: `2px solid ${selected ? '#1d4ed8' : '#e5e7eb'}`,
                  background: selected ? '#1d4ed8' : '#fff',
                  color: selected ? '#fff' : '#374151',
                  fontWeight: 600,
                  cursor: 'pointer',
                  fontSize: 13,
                }}
              >
                {option.label}
              </button>
            );
          })}
        </div>
        {/* Secondary escape hatch for uncommon dates outside the quick choices. */}
        <div className="seat-request-row">
          <label>Another workday
            <input
              type="date"
              value={date}
              min={toLocalDateString(dateBase)}
              onChange={(e) => setDate(e.target.value)}
              aria-label="Seat date"
            />
          </label>
          <button className="btn-primary" disabled={submitting || !date} onClick={() => { void requestSeat(); }}>
            {submitting ? 'Requesting…' : 'Request a seat'}
          </button>
        </div>
        {flash && <p className="plat-muted" role="status">{flash}</p>}
      </section>
    </section>
  );
}
