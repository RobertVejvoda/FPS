import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchMyOutcomes, type BookingOutcomeItem } from '../api/dataHub';
import { displayLocation, displayModule, displaySlot } from '../displayLabels';
import { ModuleBadge } from '../components/ModuleBadge';

type ListState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingOutcomeItem[]; page: number; total: number }
  | { kind: 'error'; message: string };

export function BookingHistoryPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<ListState>({ kind: 'loading' });

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchMyOutcomes({ apiBaseUrl, bearerToken }, { page: 1, pageSize: 50 }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setState({ kind: 'ok', items: result.data.items, page: result.data.page, total: result.data.total });
      } else {
        setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load history.' });
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  function loadMore() {
    if (state.kind !== 'ok') return;
    const next = state.page + 1;
    fetchMyOutcomes({ apiBaseUrl, bearerToken }, { page: next, pageSize: 50 }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        setState(prev => {
          if (prev.kind !== 'ok') return prev;
          const seen = new Set(prev.items.map(i => i.bookingRequestId));
          return {
            ...prev,
            items: [...prev.items, ...result.data.items.filter(i => !seen.has(i.bookingRequestId))],
            page: result.data.page,
            total: result.data.total,
          };
        });
      }
    });
  }

  const hasMore = state.kind === 'ok' && state.items.length < state.total;
  // UX008 (#781) — show the module column only when the employee's records span
  // more than one module, so a parking-only history stays parking-simple.
  const showModule = state.kind === 'ok'
    && new Set(state.items.map(i => displayModule(i.resourceType))).size > 1;

  return (
    <div className="page-stack">
      <section className="page-hero">
        <h2>My Reservations — History</h2>
      </section>

      <section>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
          <button onClick={() => navigate('/bookings')} style={backBtn}>← Back to My Reservations</button>
        </div>

        {state.kind === 'loading' && (
          <div className="panel"><p style={{ color: '#6b7280', margin: 0 }}>Loading…</p></div>
        )}
        {state.kind === 'error' && (
          <div className="panel">
            <p style={{ color: '#b91c1c' }}>{state.message}</p>
            <button onClick={load} className="btn-primary">Retry</button>
          </div>
        )}
        {state.kind === 'ok' && state.items.length === 0 && (
          <div className="panel">
            <p style={{ color: '#6b7280', margin: 0 }}>No past requests found.</p>
          </div>
        )}
        {state.kind === 'ok' && state.items.length > 0 && (
          <div style={{ overflowX: 'auto' }}>
            <table style={tableStyle}>
              <thead>
                <tr>
                  <th style={thStyle}>Date</th>
                  {showModule && <th style={thStyle}>Module</th>}
                  <th style={thStyle}>Time slot</th>
                  <th style={thStyle}>Location</th>
                  <th style={thStyle}>Spot</th>
                  <th style={thStyle}>Status</th>
                  <th style={thStyle}>Reason</th>
                  <th style={thStyle}>Decided</th>
                </tr>
              </thead>
              <tbody>
                {state.items.map(b => (
                  <tr key={b.bookingRequestId} style={{ borderBottom: '1px solid #f3f4f6' }}>
                    <td style={tdStyle}>{new Date(b.date + 'T00:00:00').toLocaleDateString(undefined, { dateStyle: 'medium' })}</td>
                    {showModule && <td style={tdStyle}><ModuleBadge resourceType={b.resourceType} /></td>}
                    <td style={tdStyle}>{b.timeSlot}</td>
                    <td style={tdStyle}>{displayLocation(b.locationId) ?? '–'}</td>
                    <td style={tdStyle}>{displaySlot(b.slotId) ?? '–'}</td>
                    <td style={tdStyle}><StatusChip status={b.finalStatus} /></td>
                    <td style={tdStyle}>{b.safeReasonText ?? '–'}</td>
                    <td style={tdStyle}>{b.decidedAt ? new Date(b.decidedAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' }) : '–'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {hasMore && (
              <button onClick={loadMore} style={loadMoreBtn}>Load more</button>
            )}
          </div>
        )}
      </section>
    </div>
  );
}

function StatusChip({ status }: { status: string }) {
  const color = status === 'Allocated' || status === 'Used' ? '#166534'
    : status === 'Rejected' || status === 'Cancelled' ? '#b91c1c'
    : '#374151';
  const bg = status === 'Allocated' || status === 'Used' ? '#f0fdf4'
    : status === 'Rejected' || status === 'Cancelled' ? '#fef2f2'
    : '#f9fafb';
  return (
    <span style={{ display: 'inline-block', padding: '2px 8px', borderRadius: 12, fontSize: 12, fontWeight: 600, color, background: bg }}>
      {status}
    </span>
  );
}

const tableStyle: React.CSSProperties = { width: '100%', borderCollapse: 'collapse', fontSize: 14 };
const thStyle: React.CSSProperties = { textAlign: 'left', padding: '8px 12px', fontWeight: 600, fontSize: 12, color: '#6b7280', borderBottom: '2px solid #e5e7eb', whiteSpace: 'nowrap' };
const tdStyle: React.CSSProperties = { padding: '10px 12px', verticalAlign: 'top' };
const backBtn: React.CSSProperties = { background: 'none', border: 'none', padding: 0, fontSize: 13, fontWeight: 500, color: 'var(--brand-primary)', cursor: 'pointer' };
const loadMoreBtn: React.CSSProperties = { background: 'none', border: '1px solid #e5e7eb', borderRadius: 8, padding: '10px', fontSize: 14, fontWeight: 600, color: 'var(--brand-primary)', cursor: 'pointer', width: '100%', marginTop: 12 };
