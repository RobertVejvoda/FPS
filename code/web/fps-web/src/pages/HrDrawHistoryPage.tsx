import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchDrawHistory,
  fetchDrawOutcomes,
  fetchProjectionHealth,
  type DrawHistoryItem,
  type DrawOutcomeItem,
  type ProjectionHealthResponse,
} from '../api/dataHub';
import {
  fetchDrawStatus,
  fetchDrawLifecycle,
  triggerDraw,
  type DrawStatusResult,
  type DrawLifecycleResult,
} from '../api/bookings';
import {
  displayDate,
  displayDateTime,
  displayLocation,
  displayRequestorRef,
  displaySlot,
  formatDrawStatus,
  formatDrawRequestSummary,
  formatDrawTimestamp,
  formatLifecycleStepName,
  formatScheduleSummary,
  isTimestampInPast,
  lifecycleStepStatusColor,
} from '../displayLabels';
import { nextWorkdayOptions } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';

const LOCATION_ID = 'Prague';
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

type DrawStatusOk = Extract<DrawStatusResult, { kind: 'ok' }>;

function drawScheduleLabel(draw: DrawStatusOk): string {
  switch (draw.status) {
    case 'Completed':  return 'Draw completed';
    case 'InProgress': return 'Draw in progress';
    case 'Failed':     return 'Draw failed';
    default:
      return draw.nextDrawAt && isTimestampInPast(draw.nextDrawAt)
        ? 'Scheduled Draw time passed'
        : 'Next scheduled Draw';
  }
}

function drawScheduleTone(draw: DrawStatusOk): { background: string; border: string } {
  switch (draw.status) {
    case 'Completed':  return { background: '#f0fdf4', border: '#bbf7d0' };
    case 'InProgress': return { background: '#eff6ff', border: '#93c5fd' };
    case 'Failed':     return { background: '#fef2f2', border: '#fecaca' };
    default:
      return draw.nextDrawAt && isTimestampInPast(draw.nextDrawAt)
        ? { background: '#fef3c7', border: '#fcd34d' }
        : { background: '#dbeafe', border: '#93c5fd' };
  }
}

function outcomeColor(status: string) {
  if (status === 'Allocated') return 'var(--success)';
  if (status === 'Rejected') return 'var(--danger)';
  return 'var(--muted)';
}

type DrilldownState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ok'; outcomes: DrawOutcomeItem[]; total: number }
  | { kind: 'error'; message: string };

type PageState =
  | { kind: 'loading' }
  | { kind: 'ok'; draws: DrawHistoryItem[]; total: number }
  | { kind: 'error'; message: string };

export function HrDrawHistoryPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();

  // --- Draw controls state ---
  const [selectedChip, setSelectedChip] = useState(0);
  const [drawStatus, setDrawStatus] = useState<DrawStatusResult | null>(null);
  const [drawLoading, setDrawLoading] = useState(false);
  const [lifecycle, setLifecycle] = useState<DrawLifecycleResult | null>(null);
  const [showLifecycle, setShowLifecycle] = useState(false);
  const [recoveryReason, setRecoveryReason] = useState('');
  const [recoveryRunning, setRecoveryRunning] = useState(false);
  const [drawReason, setDrawReason] = useState('');
  const [drawRunning, setDrawRunning] = useState(false);
  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);

  const { dateBase, simulationActive } = useTenantDateContext();
  const dateChips = useMemo(() => nextWorkdayOptions(dateBase, 4, { relativeLabels: !simulationActive }), [dateBase, simulationActive]);
  const selectedDate = dateChips[selectedChip]?.date ?? dateChips[0].date;

  // --- History state ---
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [health, setHealth] = useState<ProjectionHealthResponse | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [drilldown, setDrilldown] = useState<Record<string, DrilldownState>>({});

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

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

  useEffect(() => { loadDrawData(); }, [loadDrawData]);

  const loadHistory = useCallback(() => {
    setState({ kind: 'loading' });
    fetchDrawHistory({ apiBaseUrl, bearerToken }).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setState({ kind: 'ok', draws: result.data.items, total: result.data.total });
        if (result.data.items.length === 0) {
          fetchProjectionHealth({ apiBaseUrl, bearerToken }).then(hr => {
            if (hr.kind === 'ok') setHealth(hr.data);
          });
        }
      } else {
        setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load draw history.' });
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { loadHistory(); }, [loadHistory]);

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
      loadDrawData();
      loadHistory();
    } else {
      showToast(false, 'message' in result ? result.message : 'Draw failed.');
    }
  }

  function toggleExpand(drawAttemptId: string) {
    if (expanded === drawAttemptId) {
      setExpanded(null);
      return;
    }
    setExpanded(drawAttemptId);
    if (drilldown[drawAttemptId]) return;

    setDrilldown(prev => ({ ...prev, [drawAttemptId]: { kind: 'loading' } }));
    fetchDrawOutcomes({ apiBaseUrl, bearerToken }, drawAttemptId).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      setDrilldown(prev => ({
        ...prev,
        [drawAttemptId]: result.kind === 'ok'
          ? { kind: 'ok', outcomes: result.data.outcomes, total: result.data.total }
          : { kind: 'error', message: 'message' in result ? result.message : 'Failed to load outcomes.' },
      }));
    });
  }

  const drawOk = drawStatus?.kind === 'ok' ? drawStatus : null;
  const lifecycleOk = lifecycle?.kind === 'ok' ? lifecycle : null;
  const showLifecycleSteps = lifecycleOk !== null && lifecycleOk.steps.length > 0;
  const lifecycleUnavailable = lifecycle !== null && lifecycle.kind !== 'ok' && lifecycle.kind !== 'notFound';
  const scheduleTone = drawOk ? drawScheduleTone(drawOk) : null;

  return (
    <div className="page-stack">
      <div className="page-hero">
        <div>
          <h2>Draws</h2>
          <p>Schedule, run, and review allocation draws for your tenant</p>
        </div>
      </div>

      {/* Draw controls panel */}
      <div className="panel">
        <h3 style={{ fontSize: '1rem', fontWeight: 700, margin: '0 0 0.75rem', color: '#1e293b' }}>
          Draw Schedule and Controls
        </h3>

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

        {/* Lifecycle steps */}
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
      </div>

      {/* Past Draw History */}
      <div className="panel">
        <h3 style={{ fontSize: '1rem', fontWeight: 700, margin: '0 0 0.75rem', color: '#1e293b' }}>
          Past Draw Outcomes
        </h3>

        {state.kind === 'loading' && <p style={{ color: 'var(--muted)', fontSize: 14 }}>Loading draw history…</p>}

        {state.kind === 'error' && (
          <div>
            <p style={{ color: 'var(--danger)', fontSize: 14 }}>{state.message}</p>
            <button onClick={loadHistory} className="btn-primary">Retry</button>
          </div>
        )}

        {state.kind === 'ok' && state.draws.length === 0 && (
          <div style={{ background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 6, padding: '1.5rem', textAlign: 'center' }}>
            <p style={{ color: '#1e293b', fontSize: 16, fontWeight: 600, margin: '0 0 0.5rem' }}>
              No completed Draws yet
            </p>
            <p style={{ color: 'var(--muted)', fontSize: 14, margin: 0 }}>
              Draw outcomes appear here after a Draw completes. Use the "Run Draw now" action above, or advance simulation time past the scheduled Draw time.
            </p>
            {health && (
              <p style={{ color: health.status === 'healthy' ? 'var(--success)' : 'var(--warning)', fontSize: 13, margin: '0.75rem 0 0', fontWeight: 600 }}>
                Projection status: {health.status}
                {health.lastProcessedEventAt && (
                  <span style={{ fontWeight: 400, color: 'var(--muted)', marginLeft: 8 }}>
                    · last event {displayDateTime(health.lastProcessedEventAt)}
                  </span>
                )}
                {!health.lastProcessedEventAt && (
                  <span style={{ fontWeight: 400, color: 'var(--muted)', marginLeft: 8 }}>· no events processed yet</span>
                )}
              </p>
            )}
          </div>
        )}

        {state.kind === 'ok' && state.draws.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {state.draws.map(draw => {
              const isOpen = expanded === draw.drawAttemptId;
              const dd = drilldown[draw.drawAttemptId];
              return (
                <div key={draw.drawAttemptId} style={{ border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
                  <button
                    onClick={() => toggleExpand(draw.drawAttemptId)}
                    style={{ width: '100%', background: isOpen ? 'var(--surface-soft)' : 'var(--surface)', border: 'none', cursor: 'pointer', padding: '14px 16px', textAlign: 'left' }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                        <span style={{ fontWeight: 700, fontSize: 14 }}>{displayDate(draw.date)}</span>
                        <span style={{ fontSize: 13, color: 'var(--muted)' }}>{draw.timeSlot}</span>
                        {draw.locationId && (
                          <span style={{ fontSize: 12, background: '#f1f5f9', border: '1px solid var(--border)', borderRadius: 4, padding: '2px 7px', color: '#475569' }}>
                            {displayLocation(draw.locationId) ?? draw.locationId}
                          </span>
                        )}
                        {draw.safeFailureReason && (
                          <span style={{ fontSize: 12, color: 'var(--danger)' }}>{draw.safeFailureReason}</span>
                        )}
                      </div>
                      <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                        <span style={{ fontSize: 13, color: 'var(--success)', fontWeight: 600 }}>{draw.allocatedCount} allocated</span>
                        <span style={{ fontSize: 13, color: 'var(--danger)' }}>{draw.rejectedCount} rejected</span>
                        {draw.waitlistedCount > 0 && (
                          <span style={{ fontSize: 13, color: 'var(--muted)' }}>{draw.waitlistedCount} waitlisted</span>
                        )}
                        <span style={{ fontSize: 13, color: 'var(--muted)' }}>{isOpen ? '▲' : '▼'}</span>
                      </div>
                    </div>
                    {draw.completedAt && (
                      <div style={{ marginTop: 4, fontSize: 12, color: 'var(--muted)' }}>
                        Completed {displayDateTime(draw.completedAt)}
                      </div>
                    )}
                  </button>

                  {isOpen && (
                    <div style={{ borderTop: '1px solid var(--border)', padding: '12px 16px', display: 'flex', flexDirection: 'column', gap: 8 }}>
                      {(!dd || dd.kind === 'loading') && (
                        <p style={{ color: 'var(--muted)', fontSize: 13 }}>Loading outcomes…</p>
                      )}
                      {dd?.kind === 'error' && (
                        <p style={{ color: 'var(--danger)', fontSize: 13 }}>{dd.message}</p>
                      )}
                      {dd?.kind === 'ok' && dd.outcomes.length === 0 && (
                        <p style={{ color: 'var(--muted)', fontSize: 13 }}>No outcome details available.</p>
                      )}
                      {dd?.kind === 'ok' && dd.outcomes.map(item => (
                        <div key={item.bookingRequestId} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, flexWrap: 'wrap', padding: '8px 10px', background: 'var(--surface-muted)', borderRadius: 6 }}>
                          <span
                            title="Requestor reference"
                            style={{ fontSize: 13, fontWeight: 600, background: '#f1f5f9', padding: '3px 8px', borderRadius: 4, fontFamily: 'monospace', color: '#1e293b', border: '1px solid var(--border)', letterSpacing: '0.01em' }}
                          >
                            {displayRequestorRef(item.requestorId)}
                          </span>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                            <span style={{ fontSize: 13, fontWeight: 600, color: outcomeColor(item.finalStatus) }}>{item.finalStatus}</span>
                            {item.safeReasonText && (
                              <span style={{ fontSize: 12, color: 'var(--muted)' }}>{item.safeReasonText}</span>
                            )}
                            {item.slotId && (
                              <span style={{ fontSize: 12, color: 'var(--muted)' }}>{displaySlot(item.slotId) ?? 'Assigned space'}</span>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
