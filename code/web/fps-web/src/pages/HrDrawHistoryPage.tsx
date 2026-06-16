import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import {
  fetchDrawHistory,
  type DrawHistoryItem,
} from '../api/dataHub';
import {
  fetchDrawStatus,
  triggerDraw,
  type DrawStatusResult,
} from '../api/bookings';
import {
  displayDate,
  displayDateTime,
  displayLocation,
  formatDrawStatus,
  formatDrawRequestSummary,
  formatDrawTimestamp,
  formatScheduleSummary,
} from '../displayLabels';
import { nextWorkdayOptions, toLocalDateString } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';
import { DateFilter, type RangeFilterValue } from '../components/DateFilter';

const LOCATION_ID = 'Prague';
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

type DrawStatusOk = Extract<DrawStatusResult, { kind: 'ok' }>;

type HistoryState =
  | { kind: 'loading' }
  | { kind: 'ok'; draws: DrawHistoryItem[]; total: number }
  | { kind: 'error'; message: string };

// "Upcoming" covers anything still actionable for the auditor: a future
// scheduled run, an in-progress run, or a failed run that may be retried.
// Anything else (completed, in the past, projected by DataHub) belongs in
// the Past section so the Run button is never offered for it.
function isPastStatus(status: string): boolean {
  return status === 'Completed' || status === 'Failed';
}

function outcomeText(draw: DrawHistoryItem): string {
  const parts = [`${draw.allocatedCount} allocated`, `${draw.rejectedCount} rejected`];
  if (draw.waitlistedCount > 0) parts.push(`${draw.waitlistedCount} waitlisted`);
  return parts.join(' / ');
}

function statusColor(status: string): string {
  if (status === 'Completed') return 'var(--success)';
  if (status === 'Failed') return 'var(--danger)';
  if (status === 'InProgress') return '#2563eb';
  return 'var(--muted)';
}

function scheduledTimeSlot(): string {
  return `${WORKDAY_START.slice(0, 5)}-${WORKDAY_END.slice(0, 5)}`;
}

export function HrDrawHistoryPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [selectedChip, setSelectedChip] = useState(0);
  const [drawStatus, setDrawStatus] = useState<DrawStatusResult | null>(null);
  const [drawLoading, setDrawLoading] = useState(false);
  const [drawReason, setDrawReason] = useState('');
  const [drawRunning, setDrawRunning] = useState(false);
  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);
  const [history, setHistory] = useState<HistoryState>({ kind: 'loading' });
  // Past Draws date-range filter — reuses the shared component from #476.
  const [pastRange, setPastRange] = useState<RangeFilterValue>({ presetKey: 'All' });

  const { dateBase, simulationActive } = useTenantDateContext();
  const dateChips = useMemo(
    () => nextWorkdayOptions(dateBase, 4, { relativeLabels: !simulationActive }),
    [dateBase, simulationActive],
  );
  const selectedDate = dateChips[selectedChip]?.date ?? dateChips[0].date;

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  const loadDrawData = useCallback(() => {
    setDrawLoading(true);
    setDrawStatus(null);
    fetchDrawStatus(
      { apiBaseUrl, bearerToken },
      { date: selectedDate, locationId: LOCATION_ID, timeSlotStart: WORKDAY_START, timeSlotEnd: WORKDAY_END },
    ).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      setDrawStatus(result);
      setDrawLoading(false);
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, selectedDate]);

  const loadHistory = useCallback(() => {
    setHistory({ kind: 'loading' });
    // Convert the ISO timestamps the range filter emits back to local
    // DateOnly strings. A naive .slice(0,10) shifts to the previous UTC
    // day for any tenant east of UTC (e.g. Europe/Prague at midnight
    // local serialises to 22:00 UTC of the previous day). Codex review
    // on PR #491 — same lesson as the /reports tz fix in #480.
    const fromDate = pastRange.after ? toLocalDateString(new Date(pastRange.after)) : undefined;
    const toDate = pastRange.before ? toLocalDateString(new Date(pastRange.before)) : undefined;
    fetchDrawHistory({ apiBaseUrl, bearerToken }, {
      locationId: LOCATION_ID,
      pageSize: 50,
      fromDate,
      toDate,
    }).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setHistory({ kind: 'ok', draws: result.data.items, total: result.data.total });
      } else {
        setHistory({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load draws.' });
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, pastRange]);

  useEffect(() => { loadDrawData(); }, [loadDrawData]);
  useEffect(() => { loadHistory(); }, [loadHistory]);

  async function handleRunDraw() {
    const reason = drawReason.trim();
    if (!reason) { showToast(false, 'Reason is required to run a Draw.'); return; }

    setDrawRunning(true);
    const result = await triggerDraw({ apiBaseUrl, bearerToken }, {
      locationId: LOCATION_ID,
      date: selectedDate,
      timeSlotStart: `${selectedDate}T08:00:00`,
      timeSlotEnd: `${selectedDate}T18:00:00`,
      reason,
      allowRecovery: drawStatus?.kind === 'ok' && drawStatus.status === 'Failed',
    });
    setDrawRunning(false);
    setDrawReason('');

    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'forbidden') { showToast(false, 'Not authorized to run a Draw.'); return; }
    if (result.kind === 'accepted') {
      showToast(true, result.wasAlreadyCompleted ? 'Draw was already completed.' : 'Draw started.');
      loadDrawData();
      loadHistory();
    } else {
      showToast(false, 'message' in result ? result.message : 'Draw failed.');
    }
  }

  // The selected chip is "upcoming" unless DataHub already projected a
  // completed/failed entry for the same key — in that case we surface the
  // historical row in the upcoming card too, but without a Run button.
  const upcomingHistoryMatch = useMemo(() => {
    if (history.kind !== 'ok') return undefined;
    const slot = scheduledTimeSlot();
    return history.draws.find(d => d.date === selectedDate && d.timeSlot === slot && d.locationId === LOCATION_ID);
  }, [history, selectedDate]);

  // Past = all history rows. We also include the matched upcoming row when
  // it's already completed/failed so the auditor can still find it in the
  // historical timeline; the Upcoming card already shows it once.
  const pastDraws = useMemo(() => {
    if (history.kind !== 'ok') return [];
    return history.draws.filter(d => isPastStatus(d.status));
  }, [history]);

  // Effective status used by the Run gate. DataHub's projected history is
  // the source of truth for terminal state: even if /draws/status is stale
  // (or briefly disagrees), a Completed/Failed historical entry must lock
  // the Run button. Codex review on PR #491.
  const historyStatus = upcomingHistoryMatch?.status ?? null;
  const liveStatus = drawStatus?.kind === 'ok' ? drawStatus.status : null;
  const effectiveStatus = isPastStatus(historyStatus ?? '') ? historyStatus : liveStatus;

  // Recovery on Failed is the only "run" action permitted on a row that
  // already reached a terminal state. Completed must never offer Run.
  // Anything terminal in DataHub overrides a possibly stale live status.
  const canRun = effectiveStatus !== null
    && effectiveStatus !== 'Completed'
    && effectiveStatus !== 'InProgress';
  const runLabel = effectiveStatus === 'Failed' ? 'Retry Draw' : 'Run Draw';

  return (
    <div className="page-stack">
      <div className="page-hero">
        <div>
          <h2>Draws</h2>
          <p>Plan upcoming allocation draws and review historical runs for your tenant.</p>
        </div>
      </div>

      {toast && (
        <div style={{
          padding: '0.75rem 1rem',
          borderRadius: 6,
          background: toast.ok ? '#f0fdf4' : '#fef2f2',
          border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`,
          color: toast.ok ? '#166534' : '#991b1b',
          fontSize: '0.875rem',
        }}>
          {toast.text}
        </div>
      )}

      <section className="panel">
        <h3 style={sectionTitle}>Upcoming Draws</h3>
        <p style={sectionLead}>Next planned allocation draws. Pick a day to see its schedule and run it when the deadline arrives.</p>

        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
          {dateChips.map((chip, i) => (
            <button
              key={chip.date}
              onClick={() => setSelectedChip(i)}
              style={{
                padding: '0.375rem 0.875rem',
                borderRadius: 20,
                border: 'none',
                cursor: 'pointer',
                fontSize: '0.875rem',
                background: selectedChip === i ? '#2563eb' : '#f3f4f6',
                color: selectedChip === i ? '#fff' : '#374151',
                fontWeight: selectedChip === i ? 600 : 400,
              }}
            >
              {chip.label}
            </button>
          ))}
        </div>

        {drawLoading && <p style={{ color: 'var(--muted)' }}>Loading draw status…</p>}

        {!drawLoading && drawStatus?.kind === 'ok' && (
          <UpcomingDrawCard
            status={drawStatus}
            effectiveStatus={effectiveStatus ?? drawStatus.status}
            historyMatch={upcomingHistoryMatch}
            selectedDate={selectedDate}
            canRun={canRun}
            runLabel={runLabel}
            drawReason={drawReason}
            onReasonChange={setDrawReason}
            drawRunning={drawRunning}
            onRun={() => { void handleRunDraw(); }}
          />
        )}

        {!drawLoading && drawStatus && drawStatus.kind !== 'ok' && (
          <p style={{ color: 'var(--danger)', fontSize: '0.875rem' }}>
            {'message' in drawStatus ? drawStatus.message : 'Draw schedule is unavailable.'}
          </p>
        )}
      </section>

      <section className="panel">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1rem', flexWrap: 'wrap', marginBottom: '0.5rem' }}>
          <div>
            <h3 style={sectionTitle}>Past Draws</h3>
            <p style={sectionLead}>Completed and failed runs with allocation outcomes.</p>
          </div>
          <div style={{ minWidth: 280 }}>
            <DateFilter mode="range" value={pastRange} onChange={setPastRange} dateBase={dateBase} />
          </div>
        </div>

        {history.kind === 'error' && (
          <div style={{ marginBottom: '1rem' }}>
            <p style={{ color: 'var(--danger)', fontSize: 14 }}>{history.message}</p>
            <button onClick={loadHistory} className="btn-primary">Retry</button>
          </div>
        )}

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                {['Date', 'Time', 'Location', 'Status', 'Outcome', 'Completed at', 'Run details'].map(header => (
                  <th key={header} style={th}>{header}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {history.kind === 'loading' && (
                <tr><td colSpan={7} style={tdMuted}>Loading past draws…</td></tr>
              )}
              {history.kind === 'ok' && pastDraws.length === 0 && (
                <tr><td colSpan={7} style={tdMuted}>No completed draws match the current filter.</td></tr>
              )}
              {pastDraws.map(draw => (
                <tr key={draw.drawAttemptId}>
                  <td style={tdStrong}>{displayDate(draw.date)}</td>
                  <td style={td}>{draw.timeSlot}</td>
                  <td style={td}>{displayLocation(draw.locationId) ?? 'Location not set'}</td>
                  <td style={{ ...td, color: statusColor(draw.status), fontWeight: 700 }}>{formatDrawStatus(draw.status)}</td>
                  <td style={td}>{outcomeText(draw)}</td>
                  <td style={td}>{draw.completedAt ? displayDateTime(draw.completedAt) : draw.startedAt ? displayDateTime(draw.startedAt) : '-'}</td>
                  <td style={td}><RunDetailsCell draw={draw} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}

// Issue #472: render the trigger source, runner short ref, and HR-supplied
// reason on Past Draws rows. Falls back gracefully when the projection
// pre-dates the field (legacy rows show "—" instead of an empty cell).
function RunDetailsCell({ draw }: { draw: DrawHistoryItem }): React.ReactElement {
  const source = draw.triggerSource ? humanizeTriggerSource(draw.triggerSource) : null;
  const runnerRef = shortTriggeredByRef(draw.triggeredBy);
  const headline = source && runnerRef
    ? `${source} · ${runnerRef}`
    : source ?? (runnerRef ? `Run by ${runnerRef}` : null);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {headline
        ? <span style={{ fontSize: '0.85rem' }}>{headline}</span>
        : <span style={{ color: 'var(--muted)' }}>—</span>}
      {draw.runReason && (
        <span style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>
          “{draw.runReason}”
        </span>
      )}
      {draw.safeFailureReason && (
        <span style={{ fontSize: '0.8rem', color: '#b91c1c' }}>
          {draw.safeFailureReason}
        </span>
      )}
    </div>
  );
}

function humanizeTriggerSource(source: string): string {
  const label: Record<string, string> = {
    manual: 'Manual',
    scheduled: 'Scheduled',
    recovery: 'Recovery',
    simulation: 'Simulation',
  };
  return label[source] ?? source;
}

// Operator-safe short ref derived from the TriggeredBy value. For long
// hex hashes we surface the first 6 chars uppercased — matches the audit
// workspace convention so an HR user can correlate by short ref. For
// short identifiers (e.g. "dapr-cron") we render them verbatim.
function shortTriggeredByRef(value: string | null): string | null {
  if (!value) return null;
  const compact = value.replace(/-/g, '');
  if (/^[0-9a-f]{32,}$/i.test(compact)) return compact.slice(0, 6).toUpperCase();
  return value;
}

function UpcomingDrawCard({
  status, effectiveStatus, historyMatch, selectedDate, canRun, runLabel, drawReason, onReasonChange, drawRunning, onRun,
}: {
  status: DrawStatusOk;
  // Effective status drives the badge + the no-run explanation. Prefer the
  // terminal DataHub history row over the live /draws/status if both
  // disagree — Codex review on PR #491. Without this, a stale live status
  // could still render a Run button next to a Completed draw.
  effectiveStatus: string;
  historyMatch: DrawHistoryItem | undefined;
  selectedDate: string;
  canRun: boolean;
  runLabel: string;
  drawReason: string;
  onReasonChange: (v: string) => void;
  drawRunning: boolean;
  onRun: () => void;
}) {
  const requestSummary = formatDrawRequestSummary(status.requestCount, status.demandLevel);
  const schedule = status.nextDrawAt
    ? formatDrawTimestamp(status.nextDrawAt, status.timeZone)
    : formatScheduleSummary(status.scheduleStatus, status.scheduleSource);
  const reason = status.safeMessage || status.cannotRequestReason;

  return (
    <div style={upcomingCard}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '0.75rem 1.5rem' }}>
        <Fact label="Date" value={displayDate(selectedDate)} />
        <Fact label="Time" value={scheduledTimeSlot()} />
        <Fact label="Location" value={displayLocation(LOCATION_ID) ?? 'Location not set'} />
        <Fact label="Status" value={formatDrawStatus(effectiveStatus)} valueColor={statusColor(effectiveStatus)} />
        <Fact label="Requests" value={requestSummary} />
        <Fact label="Schedule" value={schedule} />
        {historyMatch && (
          <Fact label="Outcome" value={outcomeText(historyMatch)} />
        )}
      </div>

      {reason && (
        <p style={{ marginTop: '0.75rem', fontSize: '0.85rem', color: 'var(--muted)' }}>{reason}</p>
      )}

      {canRun ? (
        <div style={{ marginTop: '1rem', display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <input
            type="text"
            placeholder={`Reason to ${runLabel === 'Retry Draw' ? 'retry' : 'run'} this Draw`}
            value={drawReason}
            onChange={e => onReasonChange(e.target.value)}
            style={{ flex: '1 1 220px', maxWidth: 360, padding: '0.45rem 0.65rem', borderRadius: 4, border: '1px solid #d1d5db', fontSize: '0.875rem' }}
          />
          <button
            onClick={onRun}
            disabled={drawRunning || !drawReason.trim()}
            style={{
              padding: '0.45rem 1rem',
              borderRadius: 4,
              border: 'none',
              cursor: drawRunning || !drawReason.trim() ? 'not-allowed' : 'pointer',
              background: runLabel === 'Retry Draw' ? '#dc2626' : '#2563eb',
              color: '#fff',
              fontSize: '0.875rem',
              opacity: drawRunning || !drawReason.trim() ? 0.5 : 1,
            }}
          >
            {drawRunning ? 'Running…' : runLabel}
          </button>
        </div>
      ) : (
        <p style={{ marginTop: '0.75rem', fontSize: '0.85rem', color: 'var(--muted)' }}>
          {effectiveStatus === 'Completed'
            ? 'This draw is already complete — see Past Draws below for the outcome.'
            : effectiveStatus === 'InProgress'
              ? 'A draw run is already in progress for this date.'
              : 'No run action is available right now.'}
        </p>
      )}
    </div>
  );
}

function Fact({ label, value, valueColor }: { label: string; value: string; valueColor?: string }) {
  return (
    <div>
      <div style={{ fontSize: '0.7rem', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>{label}</div>
      <div style={{ fontSize: '0.9rem', fontWeight: 600, color: valueColor ?? '#0f172a' }}>{value}</div>
    </div>
  );
}

const sectionTitle: React.CSSProperties = { margin: 0, fontSize: '1rem', fontWeight: 700 };
const sectionLead: React.CSSProperties = { margin: '0.25rem 0 1rem', fontSize: '0.85rem', color: 'var(--muted)' };

const upcomingCard: React.CSSProperties = {
  padding: '1rem',
  border: '1px solid var(--border)',
  borderRadius: 8,
  background: '#fafbfc',
};

const th: React.CSSProperties = {
  textAlign: 'left',
  padding: '0.65rem 0.75rem',
  fontSize: '0.75rem',
  color: 'var(--muted)',
  borderBottom: '1px solid var(--border)',
  whiteSpace: 'nowrap',
};

const td: React.CSSProperties = {
  padding: '0.7rem 0.75rem',
  borderBottom: '1px solid var(--border)',
  fontSize: '0.875rem',
  verticalAlign: 'top',
};

const tdStrong: React.CSSProperties = {
  ...td,
  fontWeight: 700,
  whiteSpace: 'nowrap',
};

const tdMuted: React.CSSProperties = {
  ...td,
  color: 'var(--muted)',
  textAlign: 'center',
};
