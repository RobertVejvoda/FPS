import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, cancelBooking, confirmUsage, fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '../api/bookings';
import { fetchMyDrawOutcomes, type MyDrawOutcomeSummary } from '../api/drawHistory';
import { BookingRow } from '../components/BookingRow';
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

// No employee-profile API yet; derive location from any loaded booking, fall back to placeholder.
const FALLBACK_LOCATION_ID = 'Prague';
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

const CHIPS = [
  { label: 'Today', offset: 0 },
  { label: 'Tomorrow', offset: 1 },
  { label: weekdayLabel(2), offset: 2 },
  { label: weekdayLabel(3), offset: 3 },
];

function sortMixed(items: BookingListItem[]): BookingListItem[] {
  const today = localDate(0);
  const todayItems = items.filter(i => i.requestedDate === today);
  const futureItems = items.filter(i => i.requestedDate > today).sort((a, b) => a.requestedDate.localeCompare(b.requestedDate));
  const pastItems = items.filter(i => i.requestedDate < today).sort((a, b) => b.requestedDate.localeCompare(a.requestedDate));
  return [...todayItems, ...futureItems, ...pastItems];
}

type ListState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingListItem[]; totalCount: number; nextCursor: string | null }
  | { kind: 'error'; message: string };

export function BookingsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<ListState>({ kind: 'loading' });
  const [busyId, setBusyId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);
  const [drawStatuses, setDrawStatuses] = useState<(DrawStatusResult | null)[]>([null, null, null, null]);
  const [drawStatusesLoading, setDrawStatusesLoading] = useState(true);
  const [myDrawOutcomes, setMyDrawOutcomes] = useState<MyDrawOutcomeSummary[]>([]);
  const stateRef = useRef(state);
  stateRef.current = state;
  const drawLocationId = state.kind === 'ok'
    ? state.items.find(i => i.locationId)?.locationId ?? FALLBACK_LOCATION_ID
    : FALLBACK_LOCATION_ID;

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchBookings({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', items: result.items, totalCount: result.totalCount, nextCursor: result.nextCursor });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load your spots.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  useEffect(() => {
    if (!bearerToken) return;
    fetchMyDrawOutcomes({ apiBaseUrl, bearerToken }).then(r => {
      if (r.kind === 'ok') setMyDrawOutcomes(r.data.draws);
    });
  }, [apiBaseUrl, bearerToken]);

  useEffect(() => {
    let cancelled = false;
    setDrawStatusesLoading(true);
    setDrawStatuses([null, null, null, null]);
    Promise.all(
      CHIPS.map(chip => fetchDrawStatus({ apiBaseUrl, bearerToken }, {
        date: localDate(chip.offset),
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

  function loadMore() {
    const cur = stateRef.current;
    if (cur.kind !== 'ok' || !cur.nextCursor) return;
    const cursor = cur.nextCursor;
    fetchBookings({ apiBaseUrl, bearerToken }, { cursor }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setState((prev) => {
          if (prev.kind !== 'ok') return prev;
          const seen = new Set(prev.items.map(i => i.requestId));
          return { ...prev, items: [...prev.items, ...result.items.filter(i => !seen.has(i.requestId))], nextCursor: result.nextCursor };
        });
      }
    });
  }

  const today = localDate(0);
  const tomorrow = localDate(1);
  const d2 = localDate(2);
  const d3 = localDate(3);
  const okState = state.kind === 'ok' ? state : null;
  const allItems = okState ? sortMixed(okState.items) : [];
  const upcomingItems = allItems.filter(i => i.requestedDate >= today);
  const bookingByDate: Record<string, BookingListItem | null> = {
    [today]: okState?.items.find(i => i.requestedDate === today) ?? null,
    [tomorrow]: okState?.items.find(i => i.requestedDate === tomorrow) ?? null,
    [d2]: okState?.items.find(i => i.requestedDate === d2) ?? null,
    [d3]: okState?.items.find(i => i.requestedDate === d3) ?? null,
  };

  return (
    <div className="page-stack">
      <section className="page-hero">
        <h2>My Spots</h2>
      </section>

      <NotificationBanner />

      {/* Four-day focus cards */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
        {CHIPS.map((chip, i) => {
          const date = localDate(chip.offset);
          const booking = bookingByDate[date] ?? null;
          return (
            <FocusCard
              key={chip.offset}
              label={chip.label}
              booking={booking}
              drawStatus={drawStatuses[i] ?? null}
              drawLoading={drawStatusesLoading}
              busy={busyId === booking?.requestId}
              onCancel={booking?.nextAction === 'cancel' ? () => handleCancel(booking.requestId) : undefined}
              onConfirm={booking?.nextAction === 'confirmUsage' ? () => handleConfirm(booking.requestId) : undefined}
              onRequest={() => navigate(`/bookings/new?date=${date}`)}
            />
          );
        })}
      </div>

      {/* Other dates */}
      <div style={{ textAlign: 'right' }}>
        <button onClick={() => navigate('/bookings/new')} style={otherDatesBtn}>
          Request for another date →
        </button>
      </div>

      {toast && (
        <div style={{ padding: '10px 16px', borderRadius: 8, background: toast.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`, color: toast.ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
          {toast.text}
        </div>
      )}

      {/* My requests — upcoming only */}
      <section>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
          <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>My requests</h3>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
            {okState && <span style={{ fontSize: 13, color: '#6b7280' }}>Showing {upcomingItems.length} of {okState.totalCount}</span>}
            <button onClick={() => navigate('/bookings/history')} style={historyLinkBtn}>History</button>
          </div>
        </div>
        {state.kind === 'loading' ? (
          <div className="panel"><p style={{ color: '#6b7280', margin: 0 }}>Loading…</p></div>
        ) : state.kind === 'error' ? (
          <div className="panel">
            <p style={{ color: '#b91c1c' }}>{state.message}</p>
            <button onClick={load} className="btn-primary">Retry</button>
          </div>
        ) : upcomingItems.length === 0 ? (
          <section className="panel">
            <h3 style={{ margin: '0 0 6px', fontSize: 16 }}>No upcoming requests</h3>
            <p style={{ color: '#6b7280', margin: 0, fontSize: 14 }}>Your upcoming spot requests will appear here.</p>
          </section>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {upcomingItems.map((b) => (
              <BookingRow
                key={b.requestId}
                booking={b}
                busy={busyId === b.requestId}
                onCancel={b.nextAction === 'cancel' ? () => handleCancel(b.requestId) : undefined}
                onConfirmUsage={b.nextAction === 'confirmUsage' ? () => handleConfirm(b.requestId) : undefined}
                onNavigate={() => navigate(`/bookings/${b.requestId}`, { state: b })}
              />
            ))}
            {okState?.nextCursor && (
              <button onClick={loadMore} style={loadMoreBtn}>Load more</button>
            )}
          </div>
        )}
      </section>

      {/* Past draw outcomes */}
      <section>
        <h3 style={{ margin: '0 0 10px', fontSize: 16, fontWeight: 700 }}>Past draw outcomes</h3>
        {myDrawOutcomes.length === 0 && (
          <div style={{ background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 8, padding: '1rem', textAlign: 'center' }}>
            <p style={{ color: '#1e293b', fontSize: 14, fontWeight: 600, margin: '0 0 0.5rem' }}>
              No past Draw outcomes yet
            </p>
            <p style={{ color: 'var(--muted)', fontSize: 13, margin: 0 }}>
              Your allocation outcomes from completed Draws will appear here. Submit a request and wait for the scheduled Draw time to see results.
            </p>
          </div>
        )}
        {myDrawOutcomes.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {myDrawOutcomes.map(d => {
              const allocated = d.myOutcome === 'Allocated';
              return (
                <div key={`${d.date}:${d.locationId}:${d.timeSlot}`} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 14px', background: '#fff', border: '1px solid var(--border)', borderLeft: `4px solid ${allocated ? 'var(--success)' : 'var(--danger)'}`, borderRadius: 8, flexWrap: 'wrap' }}>
                  <div style={{ flex: 1, minWidth: 120 }}>
                    <div style={{ fontWeight: 600, fontSize: 14 }}>{new Date(d.date + 'T00:00:00').toLocaleDateString(undefined, { dateStyle: 'medium' })}</div>
                    <div style={{ fontSize: 13, color: 'var(--muted)' }}>{d.timeSlot}{d.locationId ? ` · ${d.locationId}` : ''}</div>
                    {d.completedAt && (
                      <div style={{ fontSize: 12, color: 'var(--muted)', marginTop: 2 }}>
                        Draw completed {new Date(d.completedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
                      </div>
                    )}
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontWeight: 700, fontSize: 13, color: allocated ? 'var(--success)' : 'var(--danger)' }}>
                      {allocated ? 'Spot allocated' : 'Not selected'}
                    </div>
                    {!allocated && d.myReason && (
                      <div style={{ fontSize: 12, color: 'var(--muted)', marginTop: 2 }}>{d.myReason}</div>
                    )}
                    <div style={{ fontSize: 12, color: 'var(--muted)', marginTop: 2 }}>
                      {d.allocatedCount} of {d.totalRequests} allocated
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}

function FocusCard({ label, booking, drawStatus, drawLoading, busy, onCancel, onConfirm, onRequest }: {
  label: string;
  booking: BookingListItem | null;
  drawStatus: DrawStatusResult | null;
  drawLoading?: boolean;
  busy?: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
  onRequest?: () => void;
}) {
  const scheduleOk = drawStatus?.kind === 'ok' ? drawStatus : null;

  return (
    <div style={focusCard}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
        <div style={focusDay}>{label}</div>
        {booking && <StatusBadge status={booking.status} />}
      </div>

      {/* Draw and cut-off timing */}
      {drawLoading && <div style={{ fontSize: 12, color: '#6b7280', marginTop: 6 }}>Loading schedule…</div>}
      {!drawLoading && scheduleOk?.nextDrawAt && (
        <div style={{ fontSize: 12, color: '#6b7280', marginTop: 6 }}>
          Draw: {formatCutOffAt(scheduleOk.nextDrawAt, scheduleOk.timeZone)}
        </div>
      )}
      {!drawLoading && scheduleOk?.cutOffAt && (
        <div style={{ fontSize: 12, color: '#6b7280', marginTop: 2 }}>
          Cut-off: {formatCutOffAt(scheduleOk.cutOffAt, scheduleOk.timeZone)}
        </div>
      )}

      {booking ? (
        <>
          {displaySlot(booking.allocatedSlotId) && (
            <div style={{ fontSize: 13, fontWeight: 600, marginTop: 6 }}>Spot: {displaySlot(booking.allocatedSlotId)}</div>
          )}
          <div style={{ display: 'flex', gap: 8, marginTop: 10, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
            {onCancel && <button onClick={onCancel} disabled={busy} style={focusCancelBtn}>Cancel</button>}
            {onConfirm && <button onClick={onConfirm} disabled={busy} style={focusConfirmBtn}>Confirm usage</button>}
          </div>
        </>
      ) : drawLoading ? null
      : !scheduleOk ? (
        <div style={{ fontSize: 12, color: '#6b7280', marginTop: 8 }}>Schedule unavailable — check back later</div>
      ) : !scheduleOk.canRequest ? (
        <div style={{ fontSize: 12, color: '#6b7280', marginTop: 8 }}>
          {scheduleOk.cannotRequestReason || scheduleOk.safeMessage || 'Requests not available'}
        </div>
      ) : (
        <button onClick={onRequest} style={{ ...requestBtn, marginTop: 10, width: '100%', fontSize: 13 }}>
          Request a spot →
        </button>
      )}
    </div>
  );
}

const sectionCard: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px' };
const focusCard: React.CSSProperties = { ...sectionCard, minHeight: 100 };
const focusDay: React.CSSProperties = { fontSize: 13, fontWeight: 700, color: '#374151', textTransform: 'uppercase', letterSpacing: 0.5 };
const requestBtn: React.CSSProperties = { background: 'var(--brand-primary)', color: '#fff', border: 'none', borderRadius: 8, padding: '8px 16px', fontWeight: 600, cursor: 'pointer' };
const focusCancelBtn: React.CSSProperties = { background: '#fff', border: '1px solid #b91c1c', color: '#b91c1c', borderRadius: 6, padding: '4px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer' };
const focusConfirmBtn: React.CSSProperties = { background: '#15803d', color: '#fff', border: 'none', borderRadius: 6, padding: '4px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer' };
const loadMoreBtn: React.CSSProperties = { background: 'none', border: '1px solid #e5e7eb', borderRadius: 8, padding: '10px', fontSize: 14, fontWeight: 600, color: 'var(--brand-primary)', cursor: 'pointer', width: '100%' };
const historyLinkBtn: React.CSSProperties = { background: 'none', border: 'none', padding: 0, fontSize: 13, fontWeight: 500, color: 'var(--brand-primary)', cursor: 'pointer', textDecoration: 'underline' };
const otherDatesBtn: React.CSSProperties = { background: 'none', border: 'none', padding: 0, fontSize: 13, fontWeight: 500, color: 'var(--brand-primary)', cursor: 'pointer', textDecoration: 'underline' };
