import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchBookings, cancelBooking, confirmUsage, type BookingListItem } from '../api/bookings';
import { BookingRow } from '../components/BookingRow';

type ListState =
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingListItem[]; nextCursor: string | null }
  | { kind: 'error'; message: string };

function localDate(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function BookingsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [filter, setFilter] = useState<'upcoming' | 'recent'>('upcoming');
  const [state, setState] = useState<ListState>({ kind: 'loading' });
  const [busyId, setBusyId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ ok: boolean; text: string } | null>(null);
  const filterRef = useRef(filter);
  filterRef.current = filter;

  const load = useCallback((f: 'upcoming' | 'recent') => {
    setState({ kind: 'loading' });
    const opts = f === 'upcoming' ? { from: localDate(0) } : { to: localDate(-1) };
    fetchBookings({ apiBaseUrl, bearerToken }, opts).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', items: result.items, nextCursor: result.nextCursor });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load bookings.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(filter); }, [load, filter]);

  function showToast(ok: boolean, text: string) {
    setToast({ ok, text });
    setTimeout(() => setToast(null), 4000);
  }

  async function handleCancel(requestId: string) {
    if (!confirm('Cancel this parking request?')) return;
    setBusyId(requestId);
    const result = await cancelBooking({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') { showToast(true, 'Booking cancelled.'); load(filter); }
    else showToast(false, 'message' in result ? result.message : 'Could not cancel booking.');
  }

  async function handleConfirm(requestId: string) {
    setBusyId(requestId);
    const result = await confirmUsage({ apiBaseUrl, bearerToken }, requestId);
    setBusyId(null);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      showToast(true, result.data.wasAlreadyConfirmed ? 'Usage was already recorded.' : 'Usage confirmed.');
      load(filter);
    } else showToast(false, 'message' in result ? result.message : 'Could not confirm usage.');
  }

  return (
    <div style={page}>
      <div style={header}>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>My Bookings</h2>
        <button onClick={() => navigate('/bookings/new')} style={primaryBtn}>New request</button>
      </div>

      <div style={{ display: 'flex', gap: 0, border: '1px solid #e5e7eb', borderRadius: 8, overflow: 'hidden', alignSelf: 'flex-start' }}>
        {(['upcoming', 'recent'] as const).map((f) => (
          <button
            key={f}
            onClick={() => setFilter(f)}
            style={{ ...filterBtn, ...(filter === f ? filterActive : {}) }}
          >
            {f === 'upcoming' ? 'Upcoming' : 'Recent'}
          </button>
        ))}
      </div>

      {toast ? (
        <div style={{ padding: '10px 16px', borderRadius: 8, background: toast.ok ? '#ecfdf5' : '#fef2f2', border: `1px solid ${toast.ok ? '#bbf7d0' : '#fecaca'}`, color: toast.ok ? '#166534' : '#b91c1c', fontSize: 13, fontWeight: 500 }}>
          {toast.text}
        </div>
      ) : null}

      {state.kind === 'loading' ? (
        <p style={{ color: '#6b7280' }}>Loading…</p>
      ) : state.kind === 'error' ? (
        <div>
          <p style={{ color: '#b91c1c' }}>{state.message}</p>
          <button onClick={() => load(filter)} style={primaryBtn}>Retry</button>
        </div>
      ) : state.items.length === 0 ? (
        <p style={{ color: '#6b7280' }}>No {filter} bookings.</p>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {state.items.map((b) => (
            <BookingRow
              key={b.requestId}
              booking={b}
              busy={busyId === b.requestId}
              onCancel={b.nextAction === 'cancel' ? () => handleCancel(b.requestId) : undefined}
              onConfirmUsage={b.nextAction === 'confirmUsage' ? () => handleConfirm(b.requestId) : undefined}
            />
          ))}
        </div>
      )}
    </div>
  );
}

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const header: React.CSSProperties = { display: 'flex', alignItems: 'center', justifyContent: 'space-between' };
const primaryBtn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 8, padding: '8px 18px', fontSize: 14, fontWeight: 600, cursor: 'pointer' };
const filterBtn: React.CSSProperties = { background: '#fff', border: 'none', padding: '8px 20px', fontSize: 14, fontWeight: 500, color: '#6b7280', cursor: 'pointer' };
const filterActive: React.CSSProperties = { background: '#1d4ed8', color: '#fff' };
