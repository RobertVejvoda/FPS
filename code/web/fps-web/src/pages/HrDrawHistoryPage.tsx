import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchHrDrawOutcomes, type HrDrawOutcomeSummary } from '../api/drawHistory';

function fmtDate(iso: string) {
  return new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { dateStyle: 'medium' });
}

function outcomeColor(outcome: string) {
  if (outcome === 'Allocated') return 'var(--success)';
  if (outcome === 'Rejected') return 'var(--danger)';
  return 'var(--muted)';
}

type PageState =
  | { kind: 'loading' }
  | { kind: 'ok'; draws: HrDrawOutcomeSummary[] }
  | { kind: 'error'; message: string };

export function HrDrawHistoryPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<PageState>({ kind: 'loading' });
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchHrDrawOutcomes({ apiBaseUrl, bearerToken }).then(result => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', draws: result.data.draws });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load draw history.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  function toggleExpand(key: string) {
    setExpanded(prev => {
      const next = new Set(prev);
      next.has(key) ? next.delete(key) : next.add(key);
      return next;
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
          <p style={{ color: 'var(--muted)', fontSize: 14 }}>
            Past draws appear here after a draw has completed. Run a draw from HR Operations to see results.
          </p>
        )}

        {state.kind === 'ok' && state.draws.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {state.draws.map(draw => {
              const key = `${draw.date}:${draw.locationId}:${draw.timeSlot}`;
              const isOpen = expanded.has(key);
              return (
                <div key={key} style={{ border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
                  {/* Summary header */}
                  <button
                    onClick={() => toggleExpand(key)}
                    style={{ width: '100%', background: isOpen ? 'var(--surface-soft)' : 'var(--surface)', border: 'none', cursor: 'pointer', padding: '14px 16px', textAlign: 'left' }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                        <span style={{ fontWeight: 700, fontSize: 14 }}>{fmtDate(draw.date)}</span>
                        <span style={{ fontSize: 13, color: 'var(--muted)' }}>{draw.timeSlot}</span>
                        {draw.locationId && (
                          <span style={{ fontSize: 12, background: '#f1f5f9', border: '1px solid var(--border)', borderRadius: 4, padding: '2px 7px', color: '#475569' }}>
                            {draw.locationId}
                          </span>
                        )}
                      </div>
                      <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
                        <span style={{ fontSize: 13, color: 'var(--success)', fontWeight: 600 }}>{draw.allocatedCount} allocated</span>
                        <span style={{ fontSize: 13, color: 'var(--danger)' }}>{draw.rejectedCount} rejected</span>
                        <span style={{ fontSize: 13, color: 'var(--muted)' }}>{draw.totalRequests} total</span>
                        <span style={{ fontSize: 13, color: 'var(--muted)' }}>{isOpen ? '▲' : '▼'}</span>
                      </div>
                    </div>
                    {draw.completedAt && (
                      <div style={{ marginTop: 4, fontSize: 12, color: 'var(--muted)' }}>
                        Completed {new Date(draw.completedAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
                      </div>
                    )}
                  </button>

                  {/* Expanded outcome rows */}
                  {isOpen && (
                    <div style={{ borderTop: '1px solid var(--border)', padding: '12px 16px', display: 'flex', flexDirection: 'column', gap: 8 }}>
                      {draw.outcomes.length === 0 && (
                        <p style={{ color: 'var(--muted)', fontSize: 13 }}>No outcome details available.</p>
                      )}
                      {draw.outcomes.map(item => (
                        <div key={item.requestId} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, flexWrap: 'wrap', padding: '8px 10px', background: 'var(--surface-muted)', borderRadius: 6 }}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                            <span style={{ fontSize: 12, background: '#f1f5f9', padding: '2px 6px', borderRadius: 4, fontFamily: 'monospace', color: '#475569' }}>
                              {item.requestorRef}
                            </span>
                            <span style={{ fontSize: 11, color: 'var(--muted)', fontFamily: 'monospace' }}>
                              #{item.requestId.replace(/-/g, '').slice(-6).toUpperCase()}
                            </span>
                          </div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                            <span style={{ fontSize: 13, fontWeight: 600, color: outcomeColor(item.outcome) }}>{item.outcome}</span>
                            {item.reason && (
                              <span style={{ fontSize: 12, color: 'var(--muted)' }}>{item.reason}</span>
                            )}
                            {item.allocatedSlotId && (
                              <span style={{ fontSize: 12, color: 'var(--muted)', fontFamily: 'monospace' }}>Slot {item.allocatedSlotId}</span>
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
