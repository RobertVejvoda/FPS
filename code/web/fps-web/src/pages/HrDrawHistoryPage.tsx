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
import { nextWorkdayOptions } from '../dateOptions';
import { useTenantDateContext } from '../hooks/useTenantDateBase';

const LOCATION_ID = 'Prague';
const WORKDAY_START = '08:00:00';
const WORKDAY_END = '18:00:00';

type DrawStatusOk = Extract<DrawStatusResult, { kind: 'ok' }>;

type PageState =
  | { kind: 'loading' }
  | { kind: 'ok'; draws: DrawHistoryItem[]; total: number }
  | { kind: 'error'; message: string };

type DrawRow = {
  key: string;
  date: string;
  timeSlot: string;
  locationId: string | null;
  status: string;
  requestSummary: string;
  outcome: string;
  schedule: string;
  lastEvent: string;
  reason: string | null;
  canRun: boolean;
};

function historyKey(date: string, locationId: string | null, timeSlot: string): string {
  return `${date}|${locationId ?? ''}|${timeSlot}`;
}

function scheduledTimeSlot(): string {
  return `${WORKDAY_START.slice(0, 5)}-${WORKDAY_END.slice(0, 5)}`;
}

function drawOutcome(draw: DrawHistoryItem): string {
  const parts = [`${draw.allocatedCount} allocated`, `${draw.rejectedCount} rejected`];
  if (draw.waitlistedCount > 0) parts.push(`${draw.waitlistedCount} waitlisted`);
  return parts.join(' / ');
}

function selectedRow(draw: DrawStatusOk, selectedDate: string, historyMatch: DrawHistoryItem | undefined): DrawRow {
  if (historyMatch) {
    return historyRow(historyMatch, true);
  }

  return {
    key: `selected-${selectedDate}`,
    date: selectedDate,
    timeSlot: scheduledTimeSlot(),
    locationId: LOCATION_ID,
    status: draw.status,
    requestSummary: formatDrawRequestSummary(draw.requestCount, draw.demandLevel),
    outcome: draw.status === 'Completed' ? 'Completed result not projected yet' : '-',
    schedule: draw.nextDrawAt
      ? formatDrawTimestamp(draw.nextDrawAt, draw.timeZone)
      : formatScheduleSummary(draw.scheduleStatus, draw.scheduleSource),
    lastEvent: draw.completedAt ? displayDateTime(draw.completedAt) : draw.cutOffAt ? `Deadline ${formatDrawTimestamp(draw.cutOffAt, draw.timeZone)}` : '-',
    reason: draw.safeMessage || draw.cannotRequestReason,
    canRun: draw.status !== 'Completed' && draw.status !== 'InProgress',
  };
}

function historyRow(draw: DrawHistoryItem, isSelected = false): DrawRow {
  return {
    key: `${isSelected ? 'selected-' : ''}${draw.drawAttemptId}`,
    date: draw.date,
    timeSlot: draw.timeSlot,
    locationId: draw.locationId,
    status: draw.status,
    requestSummary: '-',
    outcome: drawOutcome(draw),
    schedule: draw.triggerSource ? `Triggered by ${draw.triggerSource}` : '-',
    lastEvent: draw.completedAt ? displayDateTime(draw.completedAt) : draw.startedAt ? displayDateTime(draw.startedAt) : '-',
    reason: draw.safeFailureReason,
    canRun: false,
  };
}

function statusColor(status: string): string {
  if (status === 'Completed') return 'var(--success)';
  if (status === 'Failed') return 'var(--danger)';
  if (status === 'InProgress') return '#2563eb';
  return 'var(--muted)';
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
  const [state, setState] = useState<PageState>({ kind: 'loading' });

  const { dateBase, simulationActive } = useTenantDateContext();
  const dateChips = useMemo(() => nextWorkdayOptions(dateBase, 4, { relativeLabels: !simulationActive }), [dateBase, simulationActive]);
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
    setState({ kind: 'loading' });
    fetchDrawHistory({ apiBaseUrl, bearerToken }, { locationId: LOCATION_ID, pageSize: 25 }).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setState({ kind: 'ok', draws: result.data.items, total: result.data.total });
      } else {
        setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load draws.' });
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

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

  const rows = useMemo(() => {
    const history = state.kind === 'ok' ? state.draws : [];
    const selectedTimeSlot = scheduledTimeSlot();
    const selectedHistoryKey = historyKey(selectedDate, LOCATION_ID, selectedTimeSlot);
    const historyMatch = history.find(draw => historyKey(draw.date, draw.locationId, draw.timeSlot) === selectedHistoryKey);
    const items: DrawRow[] = [];

    if (drawStatus?.kind === 'ok') {
      items.push(selectedRow(drawStatus, selectedDate, historyMatch));
    }

    for (const draw of history) {
      if (historyMatch && draw.drawAttemptId === historyMatch.drawAttemptId) continue;
      items.push(historyRow(draw));
    }

    return items;
  }, [drawStatus, selectedDate, state]);
  const selectedCanRun = rows[0]?.date === selectedDate && rows[0].canRun;

  return (
    <div className="page-stack">
      <div className="page-hero">
        <div>
          <h2>Draws</h2>
          <p>Review scheduled and completed allocation draws for your tenant.</p>
        </div>
      </div>

      <div className="panel">
        {toast && (
          <div style={{
            marginBottom: '1rem',
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

        <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
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

          {selectedCanRun && (
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flex: '1 1 360px', justifyContent: 'flex-end' }}>
              <input
                type="text"
                placeholder="Reason to run selected Draw"
                value={drawReason}
                onChange={e => setDrawReason(e.target.value)}
                style={{ flex: '1 1 220px', maxWidth: 360, padding: '0.45rem 0.65rem', borderRadius: 4, border: '1px solid #d1d5db', fontSize: '0.875rem' }}
              />
              <button
                onClick={() => { void handleRunDraw(); }}
                disabled={drawRunning || drawLoading || !drawReason.trim() || !(drawStatus?.kind === 'ok')}
                style={{
                  padding: '0.45rem 1rem',
                  borderRadius: 4,
                  border: 'none',
                  cursor: drawRunning || drawLoading || !drawReason.trim() || !(drawStatus?.kind === 'ok') ? 'not-allowed' : 'pointer',
                  background: '#2563eb',
                  color: '#fff',
                  fontSize: '0.875rem',
                  opacity: drawRunning || drawLoading || !drawReason.trim() || !(drawStatus?.kind === 'ok') ? 0.5 : 1,
                }}
              >
                {drawRunning ? 'Running...' : drawStatus?.kind === 'ok' && drawStatus.status === 'Failed' ? 'Retry Draw' : 'Run Draw'}
              </button>
            </div>
          )}
        </div>

        {state.kind === 'error' && (
          <div style={{ marginBottom: '1rem' }}>
            <p style={{ color: 'var(--danger)', fontSize: 14 }}>{state.message}</p>
            <button onClick={loadHistory} className="btn-primary">Retry</button>
          </div>
        )}

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                {['Date', 'Time', 'Location', 'Status', 'Requests', 'Outcome', 'Schedule / completed', 'Note'].map(header => (
                  <th key={header} style={th}>{header}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {(drawLoading || state.kind === 'loading') && rows.length === 0 && (
                <tr>
                  <td colSpan={8} style={tdMuted}>Loading draws...</td>
                </tr>
              )}

              {!drawLoading && state.kind === 'ok' && rows.length === 0 && (
                <tr>
                  <td colSpan={8} style={tdMuted}>No Draw information is available yet.</td>
                </tr>
              )}

              {drawStatus && drawStatus.kind !== 'ok' && (
                <tr>
                  <td colSpan={8} style={{ ...tdMuted, color: 'var(--danger)' }}>
                    {'message' in drawStatus ? drawStatus.message : 'Draw schedule is unavailable.'}
                  </td>
                </tr>
              )}

              {rows.map(row => (
                <tr key={row.key}>
                  <td style={tdStrong}>{displayDate(row.date)}</td>
                  <td style={td}>{row.timeSlot}</td>
                  <td style={td}>{displayLocation(row.locationId) ?? 'Location not set'}</td>
                  <td style={{ ...td, color: statusColor(row.status), fontWeight: 700 }}>{formatDrawStatus(row.status)}</td>
                  <td style={td}>{row.requestSummary}</td>
                  <td style={td}>{row.outcome}</td>
                  <td style={td}>{row.lastEvent !== '-' ? row.lastEvent : row.schedule}</td>
                  <td style={{ ...td, color: 'var(--muted)' }}>{row.reason ?? '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

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
