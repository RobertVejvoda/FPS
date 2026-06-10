import { useCallback, useEffect, useState } from 'react';
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
import { fetchProfileDisplayNames, type DisplayNameMap } from '../api/profile';
import { displayDate, displayDateTime, displayLocation, displayRequestorRef, displaySlot } from '../displayLabels';

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
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [health, setHealth] = useState<ProjectionHealthResponse | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [drilldown, setDrilldown] = useState<Record<string, DrilldownState>>({});
  const [displayNames, setDisplayNames] = useState<DisplayNameMap>({});

  const load = useCallback(() => {
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

  useEffect(() => { load(); }, [load]);

  function toggleExpand(drawAttemptId: string) {
    if (expanded === drawAttemptId) {
      setExpanded(null);
      return;
    }
    setExpanded(drawAttemptId);
    if (drilldown[drawAttemptId]) return;

    setDrilldown(prev => ({ ...prev, [drawAttemptId]: { kind: 'loading' } }));
    fetchDrawOutcomes({ apiBaseUrl, bearerToken }, drawAttemptId).then(async result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setDrilldown(prev => ({
          ...prev,
          [drawAttemptId]: { kind: 'ok', outcomes: result.data.outcomes, total: result.data.total },
        }));
        const ids = [...new Set(result.data.outcomes.map(o => o.requestorId).filter(Boolean))];
        if (ids.length > 0) {
          const names = await fetchProfileDisplayNames({ apiBaseUrl, bearerToken }, ids);
          setDisplayNames(prev => ({ ...prev, ...names }));
        }
      } else {
        setDrilldown(prev => ({
          ...prev,
          [drawAttemptId]: { kind: 'error', message: 'message' in result ? result.message : 'Failed to load outcomes.' },
        }));
      }
    });
  }

  return (
    <div className="page-stack">
      <div className="page-hero">
        <div>
          <h2>Past Draw Outcomes</h2>
          <p>Completed draws and allocation results for your tenant</p>
        </div>
      </div>

      <div className="panel">
        {state.kind === 'loading' && <p style={{ color: 'var(--muted)', fontSize: 14 }}>Loading draw history…</p>}

        {state.kind === 'error' && (
          <div>
            <p style={{ color: 'var(--danger)', fontSize: 14 }}>{state.message}</p>
            <button onClick={load} className="btn-primary">Retry</button>
          </div>
        )}

        {state.kind === 'ok' && state.draws.length === 0 && (
          <div style={{ background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 6, padding: '1.5rem', textAlign: 'center' }}>
            <p style={{ color: '#1e293b', fontSize: 16, fontWeight: 600, margin: '0 0 0.5rem' }}>
              No completed Draws yet
            </p>
            <p style={{ color: 'var(--muted)', fontSize: 14, margin: 0 }}>
              Draw outcomes appear here after a Draw completes. To run a Draw, go to <strong>HR Operations</strong> and use the "Run Draw now" action, or advance simulation time past the scheduled Draw time.
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
                            title={item.requestorId}
                            style={{ fontSize: 13, fontWeight: 600, background: '#f1f5f9', padding: '3px 8px', borderRadius: 4, fontFamily: 'monospace', color: '#1e293b', border: '1px solid var(--border)', letterSpacing: '0.01em' }}
                          >
                            {displayNames[item.requestorId] ?? displayRequestorRef(item.requestorId)}
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
