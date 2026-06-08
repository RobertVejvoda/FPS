import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, cancelBooking, confirmUsage, fetchDrawStatus, type BookingListItem, type DrawStatusResult } from '../api/bookings';
import { fetchMyDrawOutcomes, type MyDrawOutcomeSummary } from '../api/drawHistory';
import { BookingRow } from '../components/BookingRow';
import { displaySlot, displayNextDrawRun, shouldShowNextDraw, formatCutOffAt } from '../displayLabels';
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
  const [myDrawOutcomes, setMyDrawOutcomes] = useState<MyDrawOutcomeSummary[]>([]);
  const [drawStatusByDate, setDrawStatusByDate] = useState<Record<string, DrawStatusResult | null>>({});
  const [drawLoadingByDate, setDrawLoadingByDate] = useState<Record<string, boolean>>({});
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

  // Fetch draw status for each of the four focus days
  useEffect(() => {
    if (!bearerToken) return;
    const dates = [0, 1, 2, 3].map(offset => localDate(offset));
    dates.forEach(date => {
      setDrawLoadingByDate(prev => ({ ...prev, [date]: true }));
      fetchDrawStatus({ apiBaseUrl, bearerToken }, { date, locationId: drawLocationId, timeSlotStart: WORKDAY_START, timeSlotEnd: WORKDAY_END }).then((result) => {
        setDrawLoadingByDate(prev => ({ ...prev, [date]: false }));
        setDrawStatusByDate(prev => ({ ...prev, [date]: result }));
      });
    });
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
  const todayBooking = okState?.items.find(i => i.requestedDate === today) ?? null;
  const tomorrowBooking = okState?.items.find(i => i.requestedDate === tomorrow) ?? null;
  const d2Booking = okState?.items.find(i => i.requestedDate === d2) ?? null;
  const d3Booking = okState?.items.find(i => i.requestedDate === d3) ?? null;

  return (
    <div className="page-stack">
      <section className="page-hero">
        <h2>My Spots</h2>
      </section>

      {/* Important notification banner */}
      <NotificationBanner />

      {/* Four-day focus cards: Today / Tomorrow / Next two weekdays */}
      {state.kind === 'ok' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <FocusCard
            label="Today"
            booking={todayBooking}
            busy={busyId === todayBooking?.requestId}
            onCancel={todayBooking?.nextAction === 'cancel' ? () => handleCancel(todayBooking.requestId) : undefined}
            onConfirm={todayBooking?.nextAction === 'confirmUsage' ? () => handleConfirm(todayBooking.requestId) : undefined}
            onRequestForDate={() => navigate(`/bookings/new?date=${today}`)}
            drawStatus={drawStatusByDate[today]}
            drawLoading={drawLoadingByDate[today]}
          />
          <FocusCard
            label="Tomorrow"
            booking={tomorrowBooking}
            busy={busyId === tomorrowBooking?.requestId}
            onCancel={tomorrowBooking?.nextAction === 'cancel' ? () => handleCancel(tomorrowBooking.requestId) : undefined}
            onConfirm={tomorrowBooking?.nextAction === 'confirmUsage' ? () => handleConfirm(tomorrowBooking.requestId) : undefined}
            onRequestForDate={() => navigate(`/bookings/new?date=${tomorrow}`)}
            drawStatus={drawStatusByDate[tomorrow]}
            drawLoading={drawLoadingByDate[tomorrow]}
          />
          <FocusCard
            label={weekdayLabel(2)}
            booking={d2Booking}
            busy={busyId === d2Booking?.requestId}
            onCancel={d2Booking?.nextAction === 'cancel' ? () => handleCancel(d2Booking.requestId) : undefined}
            onConfirm={d2Booking?.nextAction === 'confirmUsage' ? () => handleConfirm(d2Booking.requestId) : undefined}
            onRequestForDate={() => navigate(`/bookings/new?date=${d2}`)}
            drawStatus={drawStatusByDate[d2]}
            drawLoading={drawLoadingByDate[d2]}
          />
          <FocusCard
            label={weekdayLabel(3)}
            booking={d3Booking}
            busy={busyId === d3Booking?.requestId}
            onCancel={d3Booking?.nextAction === 'cancel' ? () => handleCancel(d3Booking.requestId) : undefined}
            onConfirm={d3Booking?.nextAction === 'confirmUsage' ? () => handleConfirm(d3Booking.requestId) : undefined}
            onRequestForDate={() => navigate(`/bookings/new?date=${d3}`)}
            drawStatus={drawStatusByDate[d3]}
            drawLoading={drawLoadingByDate[d3]}
          />
        </div>
      )}

      {/* Secondary date picker for uncommon dates */}
      <section style={sectionCard}>
        <div style={{ fontWeight: 700, fontSize: 15, marginBottom: 10 }}>Request for other dates</div>
        <p style={{ fontSize: 13, color: '#6b7280', marginBottom: 10 }}>
          Need a spot for a different date? Use the date picker below.
        </p>
        <button
          onClick={() => navigate('/bookings/new')}
          style={requestBtn}
        >
          Choose a different date →
        </button>
      </section>

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

function FocusCard({ label, booking, busy, onCancel, onConfirm, onRequestForDate, drawStatus, drawLoading }: {
  label: string;
  booking: BookingListItem | null;
  busy?: boolean;
  onCancel?: () => void;
  onConfirm?: () => void;
  onRequestForDate?: () => void;
  drawStatus?: DrawStatusResult | null;
  drawLoading?: boolean;
}) {
  const scheduleOk = drawStatus?.kind === 'ok' ? drawStatus : null;

  if (!booking) {
    // No existing request - show request action and timing info
    return (
      <div style={focusCard}>
        <div style={focusDay}>{label}</div>
        <div style={{ color: '#6b7280', fontSize: 13, marginTop: 4 }}>No request yet</div>

        {/* Draw/cut-off timing when available */}
        {drawLoading && <div style={{ fontSize: 12, color: '#6b7280', marginTop: 6 }}>Loading schedule…</div>}
        {scheduleOk && (
          <div style={{ marginTop: 8, fontSize: 12 }}>
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
            {scheduleOk.demandLevel && (
              <div style={{ color: '#6b7280', marginTop: 2 }}>
                Demand: {scheduleOk.demandLevel}
              </div>
            )}
          </div>
        )}

        {/* Request action or blocked reason */}
        {scheduleOk && !scheduleOk.canRequest && scheduleOk.cannotRequestReason && (
          <div style={{ marginTop: 8, padding: '8px 10px', borderRadius: 6, background: '#fef2f2', border: '1px solid #fecaca' }}>
            <div style={{ fontSize: 12, color: '#991b1b' }}>Cannot request: {scheduleOk.cannotRequestReason}</div>
          </div>
        )}
        {scheduleOk && scheduleOk.canRequest && onRequestForDate && (
          <button
            onClick={onRequestForDate}
            style={{ ...requestBtn, marginTop: 10, width: '100%', fontSize: 13 }}
          >
            Request a spot
          </button>
        )}
        {!scheduleOk && !drawLoading && onRequestForDate && (
          <button
            onClick={onRequestForDate}
            style={{ ...requestBtn, marginTop: 10, width: '100%', fontSize: 13 }}
          >
            Request a spot
          </button>
        )}
      </div>
    );
  }

  // Existing request - show status and actions
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

      {/* Show draw/cut-off timing for waiting requests */}
      {booking.status === 'Pending' && scheduleOk && (
        <div style={{ marginTop: 6, fontSize: 12 }}>
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
const requestBtn: React.CSSProperties = { background: 'var(--brand-primary)', color: '#fff', border: 'none', borderRadius: 8, padding: '8px 16px', fontSize: 14, fontWeight: 600, cursor: 'pointer' };
const focusCancelBtn: React.CSSProperties = { background: '#fff', border: '1px solid #b91c1c', color: '#b91c1c', borderRadius: 6, padding: '4px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer' };
const focusConfirmBtn: React.CSSProperties = { background: '#15803d', color: '#fff', border: 'none', borderRadius: 6, padding: '4px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer' };
const loadMoreBtn: React.CSSProperties = { background: 'none', border: '1px solid #e5e7eb', borderRadius: 8, padding: '10px', fontSize: 14, fontWeight: 600, color: 'var(--brand-primary)', cursor: 'pointer', width: '100%' };
const historyLinkBtn: React.CSSProperties = { background: 'none', border: 'none', padding: 0, fontSize: 13, fontWeight: 500, color: 'var(--brand-primary)', cursor: 'pointer', textDecoration: 'underline' };
