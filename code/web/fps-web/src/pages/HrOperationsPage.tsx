import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchHrBookings,
  hrCancelBooking,
  fetchDrawStatus,
  fetchDrawLifecycle,
  triggerDraw,
  type HrBookingListItem,
  type DrawStatusResult,
  type DrawLifecycleResult,
} from '../api/bookings';
import {
  displayDate,
  displayLocation,
  displayRequestorRef,
  displaySlot,
  formatDrawStatus,
  formatDrawRequestSummary,
  formatDrawTimestamp,
  formatLifecycleStepName,
  formatScheduleSummary,
  humanizeHrRejection,
  isTimestampInPast,
  lifecycleStepStatusColor,
} from '../displayLabels';
import { NotificationBanner } from '../components/NotificationBanner';
import { nextWorkdayOptions } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';

const LOCATION_ID = 'Prague';
// No facilities API yet; workday slot boundaries are a known gap (UX008).
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

const STATUS_FILTERS = ['All', 'Pending', 'Allocated', 'Cancelled', 'Rejected'];

type ListState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: HrBookingListItem[]; totalCount: number; nextCursor: string | null }
  | { kind: 'error'; message: string };

type DrawStatusOk = Extract<DrawStatusResult, { kind: 'ok' }>;

function statusColor(status: string): string {
  switch (status) {
    case 'Allocated': return '#22c55e';
    case 'Pending': return '#f59e0b';
    case 'Cancelled': return '#6b7280';
    case 'Rejected': return '#ef4444';
    default: return '#6b7280';
  }
}

function drawScheduleLabel(draw: DrawStatusOk): string {
  switch (draw.status) {
    case 'Completed':
      return 'Draw completed';
    case 'InProgress':
      return 'Draw in progress';
    case 'Failed':
      return 'Draw failed';
    default:
      return draw.nextDrawAt && isTimestampInPast(draw.nextDrawAt)
        ? 'Scheduled Draw time passed'
        : 'Next scheduled Draw';
  }
}

function drawScheduleTone(draw: DrawStatusOk): { background: string; border: string } {
  switch (draw.status) {
    case 'Completed':
      return { background: '#f0fdf4', border: '#bbf7d0' };
    case 'InProgress':
      return { background: '#eff6ff', border: '#93c5fd' };
    case 'Failed':
      return { background: '#fef2f2', border: '#fecaca' };
    default:
      return draw.nextDrawAt && isTimestampInPast(draw.nextDrawAt)
        ? { background: '#fef3c7', border: '#fcd34d' }
        : { background: '#dbeafe', border: '#93c5fd' };
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
  const [lifecycle, setLifecycle] = useState<DrawLifecycleResult | null>(null);
  const [showLifecycle, setShowLifecycle] = useState(false);
  const [recoveryReason, setRecoveryReason] = useState('');
  const [recoveryRunning, setRecoveryRunning] = useState(false);

  const [busyId, setBusyId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [cancelTarget, setCancelTarget] = useState<string | null>(null);

  const [drawReason, setDrawReason] = useState('');
  const [drawRunning, setDrawRunning] = useState(false);

  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);

  const { dateBase, simulationActive } = useTenantDateContext();
  const dateChips = useMemo(() => nextWorkdayOptions(dateBase, 4, { relativeLabels: !simulationActive }), [dateBase, simulationActive]);

  const selectedDate = dateChips[selectedChip]?.date ?? dateChips[0].date;

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

  const loadDrawData = useCallback(() => {
    const opts = { date: selectedDate, locationId: LOCATION_ID, timeSlotStart: WORKDAY_START, timeSlotEnd: WORKDAY_END };
    setDrawLoading(true);
    setDrawStatus(null);
    setLifecycle(null);
    Promise.all([
      fetchDrawStatus({ apiBaseUrl, bearerToken }, opts),
      fetchDrawLifecycle({ apiBaseUrl, bearerToken }, opts),
    ]).then(([statusResult, lifecycleResult]) => {
      setDrawLoading(false);
      setDrawStatus(statusResult);
      setLifecycle(lifecycleResult);
    });
  }, [apiBaseUrl, bearerToken, selectedDate]);

  useEffect(() => {
    loadDrawData();
  }, [loadDrawData]);

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

  async function handleRunDraw(opts?: { allowRecovery?: boolean; reason?: string }) {
    const reason = (opts?.reason ?? drawReason).trim();
    if (!reason) { showToast(false, 'Reason is required to run a Draw.'); return; }
    if (opts?.allowRecovery) { setRecoveryRunning(true); } else { setDrawRunning(true); }
    const result = await triggerDraw({ apiBaseUrl, bearerToken }, {
      locationId: LOCATION_ID,
      date: selectedDate,
      timeSlotStart: `${selectedDate}T08:00:00`,
      timeSlotEnd: `${selectedDate}T18:00:00`,
      reason,
      allowRecovery: opts?.allowRecovery ?? false,
    });
    if (opts?.allowRecovery) { setRecoveryRunning(false); setRecoveryReason(''); }
    else { setDrawRunning(false); setDrawReason(''); }
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'forbidden') { showToast(false, 'Not authorized to run a Draw.'); return; }
    if (result.kind === 'accepted') {
      const { data, wasAlreadyCompleted } = result;
      let msg: string;
      let isSuccess: boolean;

      if (data.status === 'Failed') {
        msg = 'Draw ended in Failed state. Check lifecycle steps for details.';
        isSuccess = false;
      } else if (data.status === 'InProgress') {
        msg = wasAlreadyCompleted
          ? 'Draw was already completed. Showing existing results.'
          : 'Draw started. Progress will refresh below.';
        isSuccess = true;
      } else if (data.status === 'Completed') {
        msg = wasAlreadyCompleted
          ? `Draw was already completed: ${data.allocatedCount} allocated, ${data.waitlistedCount ?? 0} waitlisted, ${data.rejectedCount} rejected.`
          : `Draw complete: ${data.allocatedCount} allocated, ${data.waitlistedCount ?? 0} waitlisted, ${data.rejectedCount} rejected.`;
        isSuccess = true;
      } else {
        msg = `Draw status: ${data.status}`;
        isSuccess = true;
      }

      showToast(isSuccess, msg);
      loadBookings();
      loadDrawData();
    } else {
      showToast(false, 'message' in result ? result.message : 'Draw failed.');
    }
  }

  const drawOk = drawStatus?.kind === 'ok' ? drawStatus : null;
  const lifecycleOk = lifecycle?.kind === 'ok' ? lifecycle : null;
  const showLifecycleSteps = lifecycleOk !== null && lifecycleOk.steps.length > 0;
  const lifecycleUnavailable = lifecycle !== null && lifecycle.kind !== 'ok' && lifecycle.kind !== 'notFound';
  const scheduleTone = drawOk ? drawScheduleTone(drawOk) : null;

  return (
    <div style={{ maxWidth: 960, margin: '0 auto', padding: '1.5rem 1rem' }}>
      <h1 style={{ fontSize: '1.25rem', fontWeight: 700, marginBottom: '1rem' }}>HR Operations</h1>

      {/* Important notification banner */}
      <NotificationBanner style={{ marginBottom: '1rem' }} />

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
        {dateChips.map((chip, i) => (
          <button
            key={chip.date}
            onClick={() => setSelectedChip(i)}
            style={{ padding: '0.375rem 0.875rem', borderRadius: 20, border: 'none', cursor: 'pointer', fontSize: '0.875rem',
              background: selectedChip === i ? '#2563eb' : '#f3f4f6',
              color: selectedChip === i ? '#fff' : '#374151', fontWeight: selectedChip === i ? 600 : 400 }}
          >
            {chip.label}
          </button>
        ))}
      </div>

      {/* Draw panel */}
      <section style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: 8, padding: '1rem', marginBottom: '1.25rem' }}>
        <h2 style={{ fontSize: '1rem', fontWeight: 700, margin: '0 0 0.75rem', color: '#1e293b' }}>
          Draw Schedule and Progress
        </h2>
        {drawLoading && <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: 0 }}>Loading schedule…</p>}
        {drawOk && (
          <div style={{ marginBottom: '0.75rem' }}>
            {drawOk.nextDrawAt && scheduleTone && (
              <div style={{ background: scheduleTone.background, border: `1px solid ${scheduleTone.border}`, borderRadius: 6, padding: '0.75rem', marginBottom: '0.75rem' }}>
                <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em', marginBottom: 4 }}>
                  {drawScheduleLabel(drawOk)}
                </div>
                <div style={{ fontSize: '1rem', fontWeight: 700, color: '#1e293b' }}>
                  {formatDrawTimestamp(drawOk.nextDrawAt, drawOk.timeZone)}
                </div>
                {isTimestampInPast(drawOk.nextDrawAt) && drawOk.status !== 'Completed' && (
                  <div style={{ fontSize: '0.8rem', color: '#92400e', marginTop: 4 }}>
                    Warning: Draw should have run but may not have been triggered yet. Use "Run Draw now" below.
                  </div>
                )}
              </div>
            )}
            <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', alignItems: 'flex-start' }}>
              <div>
                <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Draw status</div>
                <div style={{ fontSize: '0.9rem', fontWeight: 600, color: drawOk.status === 'Failed' ? '#dc2626' : '#1e293b', marginTop: 2 }}>
                  {formatDrawStatus(drawOk.status)}
                </div>
                <div style={{ fontSize: '0.8rem', color: '#6b7280', marginTop: 2 }}>
                  {formatDrawRequestSummary(drawOk.requestCount, drawOk.demandLevel)}
                </div>
              </div>
              <div>
                <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Request deadline</div>
                <div style={{ fontSize: '0.9rem', fontWeight: 600, marginTop: 2,
                  color: drawOk.requestWindowStatus === 'open' ? '#166534' : drawOk.requestWindowStatus === 'closed' ? '#dc2626' : '#92400e' }}>
                  {drawOk.requestWindowStatus === 'open' ? 'Requests are open' : drawOk.requestWindowStatus === 'closed' ? 'Requests are closed' : 'Deadline unknown'}
                </div>
              </div>
              {drawOk.cutOffAt && (
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                    {isTimestampInPast(drawOk.cutOffAt) ? 'Deadline passed' : 'Deadline'}
                  </div>
                  <div style={{ fontSize: '0.9rem', color: isTimestampInPast(drawOk.cutOffAt) ? '#6b7280' : '#374151', marginTop: 2 }}>
                    {formatDrawTimestamp(drawOk.cutOffAt, drawOk.timeZone)}
                  </div>
                </div>
              )}
              <div>
                <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Schedule</div>
                <div style={{ fontSize: '0.9rem', color: '#374151', marginTop: 2 }}>{formatScheduleSummary(drawOk.scheduleStatus, drawOk.scheduleSource)}</div>
              </div>
              {drawOk.completedAt && (
                <div>
                  <div style={{ fontSize: '0.75rem', color: '#6b7280', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>Completed</div>
                  <div style={{ fontSize: '0.9rem', color: '#374151', marginTop: 2 }}>
                    {formatDrawTimestamp(drawOk.completedAt, drawOk.timeZone)}
                  </div>
                </div>
              )}
            </div>
            <div style={{ marginTop: '0.5rem', fontSize: '0.8rem', color: '#6b7280' }}>{drawOk.safeMessage}</div>

            {/* Recovery action for Failed draws */}
            {drawOk.status === 'Failed' && (
              <div style={{ marginTop: '0.75rem', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 6, padding: '0.75rem' }}>
                <p style={{ fontSize: '0.875rem', color: '#991b1b', margin: '0 0 0.5rem', fontWeight: 600 }}>
                  Draw failed
                </p>
                <p style={{ fontSize: '0.8rem', color: '#7f1d1d', margin: '0 0 0.75rem' }}>
                  The Draw did not complete. Check the lifecycle steps below for details. You can retry with recovery mode — the failed attempt is preserved for audit.
                </p>
                <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
                  <input
                    type="text"
                    placeholder="Recovery reason (required)"
                    value={recoveryReason}
                    onChange={e => setRecoveryReason(e.target.value)}
                    style={{ flex: '1 1 180px', padding: '0.4rem 0.6rem', borderRadius: 4, border: '1px solid #fca5a5', fontSize: '0.875rem' }}
                  />
                  <button
                    onClick={() => { void handleRunDraw({ allowRecovery: true, reason: recoveryReason }); }}
                    disabled={recoveryRunning || !recoveryReason.trim()}
                    style={{ padding: '0.4rem 1rem', borderRadius: 4, border: 'none',
                      cursor: recoveryRunning || !recoveryReason.trim() ? 'not-allowed' : 'pointer',
                      background: '#dc2626', color: '#fff', fontSize: '0.875rem',
                      opacity: recoveryRunning || !recoveryReason.trim() ? 0.5 : 1 }}
                  >
                    {recoveryRunning ? 'Recovering…' : 'Retry with recovery'}
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
        {!drawLoading && !drawOk && (
          <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 6, padding: '0.75rem', marginBottom: '0.75rem' }}>
            <p style={{ fontSize: '0.875rem', color: '#991b1b', margin: 0 }}>
              Draw schedule unavailable. The Draw may not be configured for this date/location/time slot, or the DataHub projection may be stale.
            </p>
          </div>
        )}

        {/* Lifecycle steps — shown when Draw has run or is running */}
        {showLifecycleSteps && (
          <div style={{ marginTop: '0.75rem', borderTop: '1px solid #e2e8f0', paddingTop: '0.75rem' }}>
            <button
              onClick={() => setShowLifecycle(v => !v)}
              style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0, display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: showLifecycle ? '0.5rem' : 0 }}
            >
              <span style={{ fontSize: '0.8rem', fontWeight: 600, color: '#475569', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                Execution steps
              </span>
              <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>{showLifecycle ? '▲' : '▼'}</span>
            </button>
            {showLifecycle && (
              <ol style={{ margin: 0, padding: '0 0 0 0.25rem', listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
                {lifecycleOk!.steps.map((step, i) => (
                  <li key={i} style={{ display: 'flex', alignItems: 'flex-start', gap: '0.625rem' }}>
                    <span style={{ width: 10, height: 10, borderRadius: '50%', background: lifecycleStepStatusColor(step.status), flexShrink: 0, marginTop: 4 }} />
                    <div>
                      <span style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>{formatLifecycleStepName(step.name)}</span>
                      {step.summary && <span style={{ fontSize: '0.8rem', color: '#6b7280', marginLeft: '0.375rem' }}>{step.summary}</span>}
                      {step.errorMessage && <div style={{ fontSize: '0.8rem', color: '#dc2626', marginTop: 2 }}>{step.errorMessage}</div>}
                      {step.occurredAt && <div style={{ fontSize: '0.75rem', color: '#94a3b8', marginTop: 1 }}>{new Date(step.occurredAt).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' })}</div>}
                    </div>
                  </li>
                ))}
              </ol>
            )}
          </div>
        )}

        {/* Lifecycle unavailable message */}
        {lifecycleUnavailable && (
          <div style={{ marginTop: '0.75rem', fontSize: '0.8rem', color: '#92400e',
            background: '#fffbeb', border: '1px solid #fcd34d', borderRadius: 4, padding: '0.5rem 0.75rem' }}>
            Lifecycle details are unavailable.
          </div>
        )}

        {/* Manual trigger */}
        {drawOk?.status !== 'Failed' && (
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
        )}
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
            {listState.totalCount} request{listState.totalCount !== 1 ? 's' : ''} for {displayDate(selectedDate)}
          </p>
          {listState.items.length === 0 && (
            <div style={{ background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 6, padding: '1rem', textAlign: 'center' }}>
              <p style={{ color: '#6b7280', fontSize: '0.875rem', margin: 0 }}>
                {statusFilter !== 'All'
                  ? `No requests with status "${statusFilter}" for ${displayDate(selectedDate)}.`
                  : `No requests for ${displayDate(selectedDate)} yet. Requests will appear here as employees submit them.`
                }
              </p>
            </div>
          )}
          <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {listState.items.map(item => (
              <li key={item.requestId} style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 6, padding: '0.75rem 1rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.5rem' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
                    <span
                      title={item.requestorRef}
                      style={{ fontSize: '0.75rem', background: '#f1f5f9', padding: '0.2rem 0.5rem', borderRadius: 4, color: '#475569' }}
                    >
                      {displayRequestorRef(item.requestorRef)}
                    </span>
                    <span style={{ fontSize: '0.7rem', color: '#94a3b8', fontFamily: 'monospace' }}>
                      #{item.requestId.replace(/-/g, '').slice(-6).toUpperCase()}
                    </span>
                    <span style={{ fontWeight: 600, fontSize: '0.875rem', color: statusColor(item.status) }}>{item.status}</span>
                    <span style={{ fontSize: '0.8rem', color: '#6b7280' }}>{displayLocation(item.locationId) ?? displayLocation(LOCATION_ID) ?? 'Location not set'}</span>
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
                {(item.reasonCode || item.reason) && (
                  <p style={{ marginTop: '0.25rem', fontSize: '0.8rem', color: '#6b7280' }}>
                    {humanizeHrRejection(item.reasonCode, item.reason)}
                  </p>
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
