import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, cancelBooking, confirmUsage, fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '../api/bookings';
import { displaySlot, formatCutOffAt } from '../displayLabels';
import { StatusBadge } from '../components/StatusBadge';
import { NotificationBanner } from '../components/NotificationBanner';

function localDate(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function weekdayLabel(offsetDays: number): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return d.toLocaleDateString(undefined, { weekday: 'long' });
}

const FALLBACK_LOCATION_ID = 'Prague';
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

const DAYS = [
  { label: 'Today', offset: 0 },
  { label: 'Tomorrow', offset: 1 },
  { label: weekdayLabel(2), offset: 2 },
];

type LoadState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingListItem[] }
  | { kind: 'error'; message: string };

export function BookingsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [loadState, setLoadState] = useState<LoadState>({ kind: 'loading' });
  const [busyId, setBusyId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);
  const [drawStatuses, setDrawStatuses] = useState<(DrawStatusResult | null)[]>([null, null, null]);
  const [drawStatusesLoading, setDrawStatusesLoading] = useState(true);

  const drawLocationId = loadState.kind === 'ok'
    ? loadState.items.find(i => i.locationId)?.locationId ?? FALLBACK_LOCATION_ID
    : FALLBACK_LOCATION_ID;

  const load = useCallback(() => {
    setLoadState({ kind: 'loading' });
    fetchBookings({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setLoadState({ kind: 'ok', items: result.items });
      else setLoadState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load your spots.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    let cancelled = false;
    setDrawStatusesLoading(true);
    setDrawStatuses([null, null, null]);
    Promise.all(
      DAYS.map(day => fetchDrawStatus({ apiBaseUrl, bearerToken }, {
        date: localDate(day.offset),
        locationId: drawLocationId,
        timeSlotStart: WORKDAY_START,
        timeSlotEnd: WORKDAY_END,
      }))
    ).then(results => {
      if (cancelled) return;
      setDrawStatuses(results);
      setDrawStatusesLoading(false);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, drawLocationId]);

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  async function handleCancel(requestId: string) {
    if (!confirm('Cancel this spot request?')) return;
    setBusyId(requestId);
    const result = await cancelBooking({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') { showToast(true, 'Request cancelled.'); load(); }
    else showToast(false, 'message' in result ? result.message : 'Could not cancel this request.');
  }

  async function handleConfirm(requestId: string) {
    setBusyId(requestId);
    const result = await confirmUsage({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      showToast(true, result.data.wasAlreadyConfirmed ? 'Usage was already recorded.' : 'Usage confirmed.');
      load();
    } else showToast(false, 'message' in result ? result.message : 'Could not confirm usage.');
  }

  const items = loadState.kind === 'ok' ? loadState.items : [];

  return (
    <div className="page-stack">
      <section className="page-hero">
        <h2>My Spots</h2>
      </section>

      <NotificationBanner />

      {toast && (
        <div style={{ padding: '10px 16px', borderRadius: 8, background: toast.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`, color: toast.ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
          {toast.text}
        </div>
      )}

      {loadState.kind === 'error' && (
        <div className="panel">
          <p style={{ color: '#b91c1c', margin: '0 0 8px' }}>{loadState.message}</p>
          <button onClick={load} className="btn-primary">Retry</button>
        </div>
      )}

      {/* Three-day tiles */}
      <div className="day-tiles-grid">
        {DAYS.map((day, i) => {
          const date = localDate(day.offset);
          const booking = items.find(b => b.requestedDate === date) ?? null;
          return (
            <DayTile
              key={day.offset}
              label={day.label}
              date={date}
              booking={booking}
              drawStatus={drawStatuses[i] ?? null}
              drawLoading={loadState.kind === 'loading' || drawStatusesLoading}
              busy={busyId === booking?.requestId}
              onCancel={booking?.nextAction === 'cancel' ? () => handleCancel(booking.requestId) : undefined}
              onConfirm={booking?.nextAction === 'confirmUsage' ? () => handleConfirm(booking.requestId) : undefined}
              onRequest={() => navigate(`/bookings/new?date=${date}`)}
              onDetails={booking ? () => navigate(`/bookings/${booking.requestId}`, { state: booking }) : undefined}
            />
          );
        })}
      </div>

      {/* Secondary navigation */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 13 }}>
        <button onClick={() => navigate('/bookings/new')} style={linkBtn}>
          Request for another date →
        </button>
        <button onClick={() => navigate('/bookings/history')} style={linkBtn}>
          History &amp; all requests →
        </button>
      </div>
    </div>
  );
}

function DayTile({ label, date, booking, drawStatus, drawLoading, busy, onCancel, onConfirm, onRequest, onDetails }: {
  label: string;
  date: string;
  booking: BookingListItem | null;
  drawStatus: DrawStatusResult | null;
  drawLoading?: boolean;
  busy?: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
  onRequest?: () => void;
  onDetails?: () => void;
}) {
  const scheduleOk = drawStatus?.kind === 'ok' ? drawStatus : null;
  const d = new Date(date + 'T00:00:00');
  const dateLabel = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

  return (
    <div style={tileStyle}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 4 }}>
        <div>
          <div style={tileDayStyle}>{label}</div>
          <div style={{ fontSize: 11, color: '#9ca3af', marginTop: 1 }}>{dateLabel}</div>
        </div>
        {booking && <StatusBadge status={booking.status} />}
      </div>

      {/* Allocated spot */}
      {booking && displaySlot(booking.allocatedSlotId) && (
        <div style={{ fontSize: 13, fontWeight: 600, color: '#374151', marginTop: 6 }}>
          Spot: {displaySlot(booking.allocatedSlotId)}
        </div>
      )}

      {/* Draw/schedule timing */}
      {drawLoading && <div style={{ fontSize: 11, color: '#9ca3af', marginTop: 6 }}>Loading schedule…</div>}
      {!drawLoading && scheduleOk?.nextDrawAt && (
        <div style={{ fontSize: 11, color: '#6b7280', marginTop: 6 }}>
          Draw: {formatCutOffAt(scheduleOk.nextDrawAt, scheduleOk.timeZone)}
        </div>
      )}
      {!drawLoading && scheduleOk?.cutOffAt && (
        <div style={{ fontSize: 11, color: '#6b7280', marginTop: 2 }}>
          Cut-off: {formatCutOffAt(scheduleOk.cutOffAt, scheduleOk.timeZone)}
        </div>
      )}
      {!drawLoading && scheduleOk?.safeMessage && !booking && (
        <div style={{ fontSize: 11, color: '#6b7280', marginTop: 2 }}>{scheduleOk.safeMessage}</div>
      )}

      {/* Single primary action */}
      <div style={{ marginTop: 'auto', paddingTop: 10 }}>
        {booking ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {onConfirm && (
              <button onClick={onConfirm} disabled={busy} style={confirmBtnStyle}>
                {busy ? 'Confirming…' : 'Confirm usage'}
              </button>
            )}
            {onCancel && (
              <button onClick={onCancel} disabled={busy} style={cancelBtnStyle}>
                {busy ? 'Cancelling…' : 'Cancel'}
              </button>
            )}
            {!onCancel && !onConfirm && onDetails && (
              <button onClick={onDetails} style={detailsBtnStyle}>View details →</button>
            )}
          </div>
        ) : !drawLoading && scheduleOk?.canRequest ? (
          <button onClick={onRequest} style={requestBtnStyle}>Request a spot →</button>
        ) : !drawLoading && !scheduleOk ? null : !drawLoading && !scheduleOk?.canRequest ? (
          <div style={{ fontSize: 11, color: '#9ca3af' }}>
            {scheduleOk?.cannotRequestReason || 'Requests not open'}
          </div>
        ) : null}
      </div>
    </div>
  );
}

const tileStyle: React.CSSProperties = {
  background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8,
  padding: '14px 16px', display: 'flex', flexDirection: 'column', minHeight: 130,
};
const tileDayStyle: React.CSSProperties = { fontSize: 12, fontWeight: 700, color: '#374151', textTransform: 'uppercase', letterSpacing: 0.5 };
const requestBtnStyle: React.CSSProperties = { background: 'var(--brand-primary)', color: '#fff', border: 'none', borderRadius: 6, padding: '7px 10px', fontSize: 12, fontWeight: 600, cursor: 'pointer', width: '100%' };
const confirmBtnStyle: React.CSSProperties = { background: '#15803d', color: '#fff', border: 'none', borderRadius: 6, padding: '6px 10px', fontSize: 12, fontWeight: 600, cursor: 'pointer', width: '100%' };
const cancelBtnStyle: React.CSSProperties = { background: '#fff', border: '1px solid #b91c1c', color: '#b91c1c', borderRadius: 6, padding: '6px 10px', fontSize: 12, fontWeight: 600, cursor: 'pointer', width: '100%' };
const detailsBtnStyle: React.CSSProperties = { background: 'none', border: 'none', padding: 0, fontSize: 12, fontWeight: 500, color: 'var(--brand-primary)', cursor: 'pointer', textDecoration: 'underline' };
const linkBtn: React.CSSProperties = { background: 'none', border: 'none', padding: 0, fontSize: 13, fontWeight: 500, color: 'var(--brand-primary)', cursor: 'pointer', textDecoration: 'underline' };
