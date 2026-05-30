import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchHrBookings,
  hrCancelBooking,
  fetchDrawStatus,
  triggerDraw,
  type HrBookingListItem,
  type DrawStatusResult,
} from '../api/bookings';
import { displaySlot, formatCutOffAt } from '../displayLabels';

function localDate(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

const LOCATION_ID = 'Prague';
// No facilities API yet; workday slot boundaries are a known gap (UX008).
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

const DATE_CHIPS = [
  { label: 'Today', offset: 0 },
  { label: 'Tomorrow', offset: 1 },
  { label: 'D+2', offset: 2 },
  { label: 'D+3', offset: 3 },
];

const STATUS_FILTERS = ['All', 'Pending', 'Allocated', 'Cancelled', 'Rejected'];

type ListState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: HrBookingListItem[]; totalCount: number; nextCursor: string | null }
  | { kind: 'error'; message: string };

function statusColor(status: string): string {
  switch (status) {
    case 'Allocated': return '#22c55e';
    case 'Pending': return '#f59e0b';
    case 'Cancelled': return '#6b7280';
    case 'Rejected': return '#ef4444';
    default: return '#6b7280';
  }
}

export function HrOperationsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();

  const [selectedChip, setSelectedChip] = useState(0);
  const [statusFilter, setStatusFilter] = useState('All');
  const [listState, setListState] = useState<ListState>({ kind: 'loading' });
  const [drawStatus, setDrawStatus] = useState<DrawStatusResult | null>(null);
  const [drawLoading, setDrawLoading] = useState(false);

  const [busyId, setBusyId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);

  const [drawReason, setDrawReason] = useState('');
  const [drawRunning, setDrawRunning] = useState(false);

  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);

  const selectedDate = localDate(selectedChip);

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  const loadBookings = useCallback(() => {
    setListState({ kind: 'loading' });
    const filter = statusFilter === 'All' ? undefined : statusFilter;
    fetchHrBookings({ apiBaseUrl, bearerToken }, { locationId: LOCATION_ID, from: selectedDate, to: selectedDate, status: filter }).then((result) => {
      if (result.kind === 'unauthenticated' || result.kind === 'forbidden') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setListState({ kind: 'ok', items: result.items, totalCount: result.totalCount, nextCursor: result.nextCursor });
      else setListState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load operations queue.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, selectedDate, statusFilter]);

  useEffect(() => { loadBookings(); }, [loadBookings]);

  useEffect(() => {
    let cancelled = false;
    setDrawLoading(true);
    setDrawStatus(null);
    fetchDrawStatus({ apiBaseUrl, bearerToken }, { date: selectedDate, locationId: LOCATION_ID, timeSlotStart: WORKDAY_START, timeSlotEnd: WORKDAY_END }).then((result) => {
      if (cancelled) return;
      setDrawLoading(false);
      setDrawStatus(result);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, selectedDate]);

  async function handleHrCancel() {
    if (!cancelTarget || !cancelReason.trim()) return;
    setBusyId(cancelTarget);
    const result = await hrCancelBooking({ apiBaseUrl, bearerToken }, cancelTarget, cancelReason.trim());
    setBusyId(null);
    setCancelTarget(null);
    setCancelReason('');
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') { showToast(true, 'Request cancelled. Employee notified.'); loadBookings(); }
    else showToast(false, 'message' in result ? result.message : 'Cancel failed.');
  }

  async function handleRunDraw() {
    if (!drawReason.trim()) { showToast(false, 'Reason is required to run a Draw.'); return; }
    setDrawRunning(true);
    const result = await triggerDraw({ apiBaseUrl, bearerToken }, {
      locationId: LOCATION_ID,
      date: selectedDate,
      timeSlotStart: `${selectedDate}T08:00:00`,
      timeSlotEnd: `${selectedDate}T18:00:00`,
      reason: drawReason.trim(),
    });
    setDrawRunning(false);
    setDrawReason('');
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'forbidden') { showToast(false, 'Not authorized to run a Draw.'); return; }
    if (result.kind === 'accepted') {
      const { data } = result;
      showToast(true, `Draw complete: ${data.allocatedCount} allocated, ${data.waitlistedCount ?? 0} waitlisted, ${data.rejectedCount} rejected.`);
      loadBookings();
    } else {
      showToast(false, 'message' in result ? result.message : 'Draw failed.');
    }
  }

  const drawOk = drawStatus?.kind === 'ok' ? drawStatus : null;

  return (
    <div style={{ maxWidth: 960, margin: '0 auto', padding: '1.5rem 1rem' }}>
      <h1 style={{ fontSize: '1.25rem', fontWeight: 700, marginBottom: '1rem' }}>HR Operations</h1>

      {toast && (
        <div style={{ marginBottom: '1rem', padding: '0.75rem 1rem', borderRadius: 6,
          background: toast.ok ? '#f0fdf4' : '#fef2f2',
          border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`,
          color: toast.ok ? '#166534' : '#991b1b', fontSize: '0.875rem' }}>
          {toast.text}
        </div>
      )}

      {/* Date picker */}
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
        {DATE_CHIPS.map((chip, i) => (
          <button
            key={chip.offset}
            onClick={() => setSelectedChip(i)}
            style={{ padding: '0.375rem 0.875rem', borderRadius: 20, border: 'none', cursor: 'pointer', fontSize: '0.875rem',
              background: selectedChip === i ? '#2563eb' : '#f3f4f6',
              color: selectedChip === i ? '#fff' : '#374151', fontWeight: selectedChip === i ? 600 : 400 }}
          >
            {chip.label}
          </button>
        ))}
        <span style={{ alignSelf: 'center', fontSize: '0.8rem', color: '#6b7280' }}>{selectedDate}</span>
      </div>

      {/* Draw panel (DRAW005) */}
      <section style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: 8, padding: '1rem', marginBottom: '1.25rem' }}>
        {drawLoading && <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: 0 }}>Loading schedule…</p>}
        {drawOk && (
          <div style={{ marginBottom: '0.75rem' }}>
            <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', alignItems: 'flex-start' }}>
              <div>
                <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Draw status</div>
                <div style={{ fontSize: '0.9rem', fontWeight: 600, color: '#1e293b', marginTop: 2 }}>
                  {drawOk.status} · {drawOk.requestCount} request{drawOk.requestCount !== 1 ? 's' : ''} · demand: {drawOk.demandLevel}
                </div>
              </div>
              <div>
                <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Request window</div>
                <div style={{ fontSize: '0.9rem', fontWeight: 600, marginTop: 2,
                  color: drawOk.requestWindowStatus === 'open' ? '#166534' : drawOk.requestWindowStatus === 'closed' ? '#dc2626' : '#92400e' }}>
                  {drawOk.requestWindowStatus === 'open' ? 'Open' : drawOk.requestWindowStatus === 'closed' ? 'Closed' : 'Unknown'}
                </div>
              </div>
              {drawOk.nextDrawAt && (
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Next draw</div>
                  <div style={{ fontSize: '0.9rem', color: '#374151', marginTop: 2 }}>{formatCutOffAt(drawOk.nextDrawAt, drawOk.timeZone)}</div>
                </div>
              )}
              {drawOk.cutOffAt && (
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Cut-off</div>
                  <div style={{ fontSize: '0.9rem', color: '#374151', marginTop: 2 }}>{formatCutOffAt(drawOk.cutOffAt, drawOk.timeZone)}</div>
                </div>
              )}
              <div>
                <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Schedule</div>
                <div style={{ fontSize: '0.9rem', color: '#374151', marginTop: 2 }}>{drawOk.scheduleStatus} · {drawOk.scheduleSource}</div>
              </div>
            </div>
            <div style={{ marginTop: '0.5rem', fontSize: '0.8rem', color: '#6b7280' }}>{drawOk.safeMessage}</div>
          </div>
        )}
        {!drawLoading && !drawOk && (
          <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: '0 0 0.75rem' }}>Schedule unavailable.</p>
        )}
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem' }}>
          <div />
        </div>
        <div style={{ marginTop: '0.75rem', display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <input
            type="text"
            placeholder="Reason (required)"
            value={drawReason}
            onChange={e => setDrawReason(e.target.value)}
            style={{ flex: '1 1 200px', padding: '0.4rem 0.6rem', borderRadius: 4, border: '1px solid #d1d5db', fontSize: '0.875rem' }}
          />
          <button
            onClick={() => { void handleRunDraw(); }}
            disabled={drawRunning || !drawReason.trim()}
            style={{ padding: '0.4rem 1rem', borderRadius: 4, border: 'none', cursor: drawRunning || !drawReason.trim() ? 'not-allowed' : 'pointer',
              background: '#2563eb', color: '#fff', fontSize: '0.875rem', opacity: drawRunning || !drawReason.trim() ? 0.5 : 1 }}
          >
            {drawRunning ? 'Running…' : 'Run Draw now'}
          </button>
        </div>
      </section>

      {/* Status filter */}
      <div style={{ display: 'flex', gap: '0.375rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
        {STATUS_FILTERS.map(s => (
          <button
            key={s}
            onClick={() => setStatusFilter(s)}
            style={{ padding: '0.25rem 0.75rem', borderRadius: 12, border: `1px solid ${statusFilter === s ? '#2563eb' : '#d1d5db'}`,
              background: statusFilter === s ? '#eff6ff' : '#fff', color: statusFilter === s ? '#2563eb' : '#374151',
              fontSize: '0.8rem', cursor: 'pointer' }}
          >
            {s}
          </button>
        ))}
      </div>

      {/* Request list */}
      {listState.kind === 'loading' && <p style={{ color: '#6b7280', fontSize: '0.875rem' }}>Loading queue…</p>}
      {listState.kind === 'error' && <p style={{ color: '#ef4444', fontSize: '0.875rem' }}>{listState.message}</p>}
      {listState.kind === 'ok' && (
        <>
          <p style={{ fontSize: '0.8rem', color: '#6b7280', marginBottom: '0.5rem' }}>
            {listState.totalCount} request{listState.totalCount !== 1 ? 's' : ''} for {selectedDate}
          </p>
          {listState.items.length === 0 && (
            <p style={{ color: '#6b7280', fontSize: '0.875rem' }}>No requests for this date{statusFilter !== 'All' ? ` with status "${statusFilter}"` : ''}.</p>
          )}
          <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {listState.items.map(item => (
              <li key={item.requestId} style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 6, padding: '0.75rem 1rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                    <span style={{ fontSize: '0.75rem', background: '#f1f5f9', padding: '0.2rem 0.5rem', borderRadius: 4, fontFamily: 'monospace', color: '#475569' }}>
                      {item.requestorRef}…
                    </span>
                    <span style={{ fontWeight: 600, fontSize: '0.875rem', color: statusColor(item.status) }}>{item.status}</span>
                    {item.locationId && <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>{item.locationId}</span>}
                    {item.allocatedSlotId && (
                      <span style={{ fontSize: '0.8rem', color: '#374151' }}>{displaySlot(item.allocatedSlotId)}</span>
                    )}
                  </div>
                  {(item.status === 'Pending' || item.status === 'Allocated') && (
                    <button
                      disabled={busyId === item.requestId}
                      onClick={() => setCancelTarget(item.requestId)}
                      style={{ padding: '0.25rem 0.75rem', borderRadius: 4, border: '1px solid #fca5a5',
                        background: '#fff', color: '#dc2626', fontSize: '0.8rem', cursor: 'pointer' }}
                    >
                      Cancel
                    </button>
                  )}
                </div>
                {item.reasonCode && (
                  <p style={{ marginTop: '0.25rem', fontSize: '0.8rem', color: '#6b7280' }}>{item.reasonCode}</p>
                )}
              </li>
            ))}
          </ul>
        </>
      )}

      {/* Cancel modal */}
      {cancelTarget && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 }}>
          <div style={{ background: '#fff', borderRadius: 8, padding: '1.5rem', width: '100%', maxWidth: 420, margin: '0 1rem' }}>
            <h2 style={{ fontSize: '1rem', fontWeight: 700, marginBottom: '0.75rem' }}>Cancel request</h2>
            <p style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '0.75rem' }}>
              The employee will be notified. An audit record will be created.
            </p>
            <textarea
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              placeholder="Reason (required)"
              rows={3}
              style={{ width: '100%', padding: '0.5rem', borderRadius: 4, border: '1px solid #d1d5db', fontSize: '0.875rem', boxSizing: 'border-box', resize: 'vertical' }}
            />
            <div style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem', justifyContent: 'flex-end' }}>
              <button
                onClick={() => { setCancelTarget(null); setCancelReason(''); }}
                style={{ padding: '0.4rem 1rem', borderRadius: 4, border: '1px solid #d1d5db', background: '#fff', cursor: 'pointer', fontSize: '0.875rem' }}
              >
                Back
              </button>
              <button
                disabled={!cancelReason.trim() || busyId === cancelTarget}
                onClick={() => { void handleHrCancel(); }}
                style={{ padding: '0.4rem 1rem', borderRadius: 4, border: 'none', background: '#dc2626', color: '#fff',
                  cursor: !cancelReason.trim() ? 'not-allowed' : 'pointer', fontSize: '0.875rem',
                  opacity: !cancelReason.trim() ? 0.5 : 1 }}
              >
                {busyId === cancelTarget ? 'Cancelling…' : 'Confirm cancel'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
