import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, type BookingListItem } from '../api/bookings';

type ListState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingListItem[]; nextCursor: string | null }
  | { kind: 'error'; message: string };

function localDate(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function BookingHistoryPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<ListState>({ kind: 'loading' });

  const yesterday = localDate(-1);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchBookings({ apiBaseUrl, bearerToken }, { to: yesterday }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        const past = result.items.filter(i => i.requestedDate <= yesterday);
        past.sort((a, b) => b.requestedDate.localeCompare(a.requestedDate));
        setState({ kind: 'ok', items: past, nextCursor: result.nextCursor });
      } else {
        setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load history.' });
      }
    });
  }, [apiBaseUrl, bearerToken, clear, navigate, yesterday]);

  useEffect(() => { load(); }, [load]);

  function loadMore() {
    if (state.kind !== 'ok' || !state.nextCursor) return;
    const cursor = state.nextCursor;
    fetchBookings({ apiBaseUrl, bearerToken }, { cursor, to: yesterday }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') {
        const past = result.items.filter(i => i.requestedDate <= yesterday);
        past.sort((a, b) => b.requestedDate.localeCompare(a.requestedDate));
        setState(prev => {
          if (prev.kind !== 'ok') return prev;
          const seen = new Set(prev.items.map(i => i.requestId));
          return { ...prev, items: [...prev.items, ...past.filter(i => !seen.has(i.requestId))], nextCursor: result.nextCursor };
        });
      }
    });
  }

  return (
    <div className="page-stack">
      <section className="page-hero">
        <h2>My Spots — History</h2>
      </section>

      <section>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
          <button onClick={() => navigate('/bookings')} style={backBtn}>← Back to My Spots</button>
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
                  <th style={thStyle}>Time slot</th>
                  <th style={thStyle}>Location</th>
                  <th style={thStyle}>Status</th>
                  <th style={thStyle}>Reason</th>
                  <th style={thStyle}>Last change</th>
                </tr>
              </thead>
              <tbody>
                {state.items.map(b => (
                  <tr key={b.requestId} style={{ borderBottom: '1px solid #f3f4f6' }}>
                    <td style={tdStyle}>{new Date(b.requestedDate + 'T00:00:00').toLocaleDateString(undefined, { dateStyle: 'medium' })}</td>
                    <td style={tdStyle}>{b.timeSlotStart.slice(0, 5)}–{b.timeSlotEnd.slice(0, 5)}</td>
                    <td style={tdStyle}>{b.locationId ?? '–'}</td>
                    <td style={tdStyle}><StatusChip status={b.status} /></td>
                    <td style={tdStyle}>{b.reason ?? '–'}</td>
                    <td style={tdStyle}>{new Date(b.lastStatusChangedAt).toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' })}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {state.nextCursor && (
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
