import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, cancelBooking, confirmUsage, fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '../api/bookings';
import { fetchMyDrawOutcomes, type MyDrawOutcomeSummary } from '../api/drawHistory';
import { BookingRow } from '../components/BookingRow';
import { displaySlot, displayNextDrawRun, shouldShowNextDraw, formatCutOffAt } from '../displayLabels';
import { StatusBadge } from '../components/StatusBadge';

function localDate(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

// No employee-profile API yet; derive location from any loaded booking, fall back to placeholder.
const FALLBACK_LOCATION_ID = 'Prague';
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

const CHIPS = [
  { label: 'Today', offset: 0 },
  { label: 'Tomorrow', offset: 1 },
  { label: 'D+2', offset: 2 },
  { label: 'D+3', offset: 3 },
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
  const [selectedChip, setSelectedChip] = useState(0);
  const [drawStatus, setDrawStatus] = useState<DrawStatusResult | null>(null);
  const [drawLoading, setDrawLoading] = useState(false);
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
    setDrawLoading(true);
    setDrawStatus(null);
    fetchDrawStatus({ apiBaseUrl, bearerToken }, { date: localDate(selectedChip), locationId: drawLocationId, timeSlotStart: WORKDAY_START, timeSlotEnd: WORKDAY_END }).then((result) => {
      if (cancelled) return;
      setDrawLoading(false);
      setDrawStatus(result);
    });
    return () => { cancelled = true; };
  }, [apiBaseUrl, bearerToken, selectedChip, drawLocationId]);

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
  const okState = state.kind === 'ok' ? state : null;
  const allItems = okState ? sortMixed(okState.items) : [];
  const todayBooking = okState?.items.find(i => i.requestedDate === today) ?? null;
  const tomorrowBooking = okState?.items.find(i => i.requestedDate === tomorrow) ?? null;

  const scheduleOk = drawStatus?.kind === 'ok' ? drawStatus : null;
  const demandLabel = drawLoading ? 'Loading…'
    : scheduleOk ? `Demand: ${scheduleOk.demandLevel}`
    : '–';
  const canRequestLabel = scheduleOk
    ? (scheduleOk.canRequest ? 'Can request: Yes' : `Can request: No${scheduleOk.cannotRequestReason ? ` — ${scheduleOk.cannotRequestReason}` : ''}`)
    : null;

  return (
    <div className="page-stack">
      <section className="page-hero">
        <h2>My Spots</h2>
      </section>

      {/* Today / Tomorrow focus cards */}
      {(todayBooking || tomorrowBooking || state.kind === 'ok') && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <FocusCard label="Today" booking={todayBooking} busy={busyId === todayBooking?.requestId} onCancel={todayBooking?.nextAction === 'cancel' ? () => handleCancel(todayBooking.requestId) : undefined} onConfirm={todayBooking?.nextAction === 'confirmUsage' ? () => handleConfirm(todayBooking.requestId) : undefined} />
          <FocusCard label="Tomorrow" booking={tomorrowBooking} busy={busyId === tomorrowBooking?.requestId} onCancel={tomorrowBooking?.nextAction === 'cancel' ? () => handleCancel(tomorrowBooking.requestId) : undefined} onConfirm={tomorrowBooking?.nextAction === 'confirmUsage' ? () => handleConfirm(tomorrowBooking.requestId) : undefined} />
        </div>
      )}

      {/* Quick request */}
      <section style={sectionCard}>
        <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 10 }}>Request a spot</div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 10 }}>
          {CHIPS.map((chip) => (
            <button
              key={chip.offset}
              onClick={() => setSelectedChip(chip.offset)}
              style={{ ...chipBtn, ...(selectedChip === chip.offset ? chipActive : {}) }}
            >
              {chip.label}
            </button>
          ))}
          <button onClick={() => navigate('/bookings/new')} style={chipBtn}>More</button>
        </div>
        {/* Schedule info (DRAW005) */}
        {drawLoading && <div style={{ fontSize: 13, color: '#6b7280', marginBottom: 8 }}>Loading schedule…</div>}
        {scheduleOk && (
          <div style={{ fontSize: 13, marginBottom: 8, padding: '8px 10px', borderRadius: 6,
            background: scheduleOk.requestWindowStatus === 'open' ? '#f0fdf4' : '#fafafa',
            border: `1px solid ${scheduleOk.requestWindowStatus === 'open' ? '#bbf7d0' : '#e5e7eb'}` }}>
            <div style={{ color: '#374151' }}>{scheduleOk.safeMessage}</div>
            {scheduleOk.nextDrawAt && (
              <div style={{ color: '#6b7280', marginTop: 2 }}>
                Next draw: {formatCutOffAt(scheduleOk.nextDrawAt, scheduleOk.timeZone)}
              </div>
            )}
            {scheduleOk.cutOffAt && (
              <div style={{ color: '#6b7280', marginTop: 2 }}>
                Cut-off: {formatCutOffAt(scheduleOk.cutOffAt, scheduleOk.timeZone)}
              </div>
            )}
          </div>
        )}
        {!drawLoading && !scheduleOk && (
          <div style={{ fontSize: 13, color: '#6b7280', marginBottom: 8 }}>
            {demandLabel}{canRequestLabel ? ` · ${canRequestLabel}` : ''}
          </div>
        )}
        <button
          onClick={() => navigate(`/bookings/new?date=${localDate(selectedChip)}`)}
          style={requestBtn}
        >
          Request for {CHIPS[selectedChip]?.label ?? localDate(selectedChip)} →
        </button>
      </section>

      {toast && (
        <div style={{ padding: '10px 16px', borderRadius: 8, background: toast.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`, color: toast.ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
          {toast.text}
        </div>
      )}

      {/* My requests */}
      <section>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
          <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700 }}>My requests</h3>
          {okState && <span style={{ fontSize: 13, color: '#6b7280' }}>Showing {okState.items.length} of {okState.totalCount}</span>}
        </div>
        {state.kind === 'loading' ? (
          <div className="panel"><p style={{ color: '#6b7280', margin: 0 }}>Loading…</p></div>
        ) : state.kind === 'error' ? (
          <div className="panel">
            <p style={{ color: '#b91c1c' }}>{state.message}</p>
            <button onClick={load} className="btn-primary">Retry</button>
          </div>
        ) : allItems.length === 0 ? (
          <section className="panel">
            <h3 style={{ margin: '0 0 6px', fontSize: 16 }}>No requests yet</h3>
            <p style={{ color: '#6b7280', margin: 0, fontSize: 14 }}>Your spot requests will appear here.</p>
          </section>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {allItems.map((b) => (
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
      {myDrawOutcomes.length > 0 && (
        <section>
          <h3 style={{ margin: '0 0 10px', fontSize: 16, fontWeight: 700 }}>Past draw outcomes</h3>
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
        </section>
      )}
    </div>
  );
}

function FocusCard({ label, booking, busy, onCancel, onConfirm }: {
  label: string;
  booking: BookingListItem | null;
  busy?: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
}) {
  if (!booking) {
    return (
      <div style={focusCard}>
        <div style={focusDay}>{label}</div>
        <div style={{ color: '#6b7280', fontSize: 13, marginTop: 4 }}>No request yet</div>
      </div>
    );
  }
  const slot = displaySlot(booking.allocatedSlotId);
  const nextDraw = shouldShowNextDraw(booking.status) ? displayNextDrawRun(booking.requestedDate) : null;
  return (
    <div style={focusCard}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
        <div style={focusDay}>{label}</div>
        <StatusBadge status={booking.status} />
      </div>
      {slot && <div style={{ fontSize: 13, fontWeight: 600, marginTop: 6 }}>Spot: {slot}</div>}
      {nextDraw && <div style={{ fontSize: 12, color: '#1d4ed8', marginTop: 4 }}>Next draw: {nextDraw}</div>}
      <div style={{ display: 'flex', gap: 8, marginTop: 10, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
        {onCancel && <button onClick={onCancel} disabled={busy} style={focusCancelBtn}>Cancel</button>}
        {onConfirm && <button onClick={onConfirm} disabled={busy} style={focusConfirmBtn}>Confirm usage</button>}
      </div>
    </div>
  );
}

const sectionCard: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '16px 20px' };
const focusCard: React.CSSProperties = { ...sectionCard, minHeight: 100 };
const focusDay: React.CSSProperties = { fontSize: 13, fontWeight: 700, color: '#374151', textTransform: 'uppercase', letterSpacing: 0.5 };
const chipBtn: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 20, padding: '6px 16px', fontSize: 14, fontWeight: 500, color: '#374151', cursor: 'pointer' };
const chipActive: React.CSSProperties = { background: 'var(--brand-primary)', borderColor: 'var(--brand-primary)', color: '#fff' };
const requestBtn: React.CSSProperties = { background: 'var(--brand-primary)', color: '#fff', border: 'none', borderRadius: 8, padding: '8px 16px', fontSize: 14, fontWeight: 600, cursor: 'pointer' };
const focusCancelBtn: React.CSSProperties = { background: '#fff', border: '1px solid #b91c1c', color: '#b91c1c', borderRadius: 6, padding: '4px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer' };
const focusConfirmBtn: React.CSSProperties = { background: '#15803d', color: '#fff', border: 'none', borderRadius: 6, padding: '4px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer' };
const loadMoreBtn: React.CSSProperties = { background: 'none', border: '1px solid #e5e7eb', borderRadius: 8, padding: '10px', fontSize: 14, fontWeight: 600, color: 'var(--brand-primary)', cursor: 'pointer', width: '100%' };
