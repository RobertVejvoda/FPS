import { useCallback, useEffect, useMemo, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { fetchHrBookings, type HrBookingListItem } from '../api/bookings';
import { t } from '../i18n';

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
      else if (r.kind === 'unauthenticated') setState({ kind: 'error', message: t('hr.seats.unauthorized') });
      else if (r.kind === 'forbidden') setState({ kind: 'forbidden' });
      else if (r.kind === 'unreachable') setState({ kind: 'error', message: t('hr.seats.unreachable') });
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
        <h1>{t('hr.seats.title')}</h1>
        <p className="plat-muted">{t('hr.seats.description')}</p>
      </header>

      <div className="plat-filters">
        <label>{t('hr.seats.dayLabel')}
          <input className="plat-input" type="date" value={date} onChange={(e) => setDate(e.target.value)} aria-label={t('hr.seats.dayAria')} />
        </label>
        <label>{t('hr.seats.areaLabel')}
          <input className="plat-input" value={location} onChange={(e) => setLocation(e.target.value)} aria-label={t('hr.seats.areaLabel')} />
        </label>
      </div>

      {state.kind === 'loading' && <p className="plat-muted">{t('common.loading')}</p>}
      {state.kind === 'forbidden' && <p className="plat-error" role="alert">{t('hr.seats.forbidden')}</p>}
      {state.kind === 'error' && <p className="plat-error" role="alert">{state.message}</p>}

      {state.kind === 'ok' && (
        <>
          <div className="plat-card-grid">
            <div className="plat-card"><h3>{t('hr.seats.requested')}</h3><p className="seat-metric">{summary.requested}</p><p className="plat-muted">{t('hr.seats.requestedSub')}</p></div>
            <div className="plat-card"><h3>{t('hr.seats.allocated')}</h3><p className="seat-metric">{summary.allocated}</p><p className="plat-muted">{t('hr.seats.allocatedSub')}</p></div>
            <div className="plat-card"><h3>{t('hr.seats.waitlisted')}</h3><p className="seat-metric">{summary.waitlisted}</p><p className="plat-muted">{t('hr.seats.waitlistedSub')}</p></div>
          </div>

          <div className="plat-card">
            <h3>{t('hr.seats.allocatedTitle')}</h3>
            {summary.seats.length === 0
              ? <p className="plat-muted">{t('hr.seats.noneAllocated')}</p>
              : <ul className="seat-chip-row">{summary.seats.map((s, i) => <li key={i} className="seat-chip">{s}</li>)}</ul>}
          </div>
        </>
      )}
    </section>
  );
}
