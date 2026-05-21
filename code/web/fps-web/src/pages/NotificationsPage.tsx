import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchNotifications, markNotificationRead, type NotificationItem } from '../api/notifications';

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; items: NotificationItem[] }
  | { kind: 'error'; message: string };

export function NotificationsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [busyId, setBusyId] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'unread'>('all');

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchNotifications({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', items: result.data.items });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load notifications.' });
    });
  }, [apiBaseUrl, bearerToken, clear, navigate]);

  useEffect(() => { load(); }, [load]);

  async function handleMarkRead(id: string) {
    setBusyId(id);
    const result = await markNotificationRead({ apiBaseUrl, bearerToken }, id);
    setBusyId(null);
    if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
    if (result.kind === 'ok') {
      setState(prev => prev.kind === 'ok'
        ? { kind: 'ok', items: prev.items.map(n => n.id === id ? { ...n, isRead: true } : n) }
        : prev);
    }
  }

  const visibleItems = state.kind === 'ok'
    ? (filter === 'unread' ? state.items.filter(n => !n.isRead) : state.items)
    : [];

  return (
    <div style={page}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Notifications</h2>
        <div style={{ display: 'flex', gap: 0, border: '1px solid #e5e7eb', borderRadius: 8, overflow: 'hidden' }}>
          {(['all', 'unread'] as const).map(f => (
            <button key={f} onClick={() => setFilter(f)}
              style={{ background: filter === f ? '#1d4ed8' : '#fff', color: filter === f ? '#fff' : '#6b7280', border: 'none', padding: '7px 18px', fontSize: 13, fontWeight: 500, cursor: 'pointer' }}>
              {f === 'all' ? 'All' : 'Unread'}
            </button>
          ))}
        </div>
      </div>

      {state.kind === 'loading' && <p style={muted}>Loading…</p>}
      {state.kind === 'error' && (
        <div>
          <p style={{ color: '#b91c1c' }}>{state.message}</p>
          <button onClick={load} style={btn}>Retry</button>
        </div>
      )}
      {state.kind === 'ok' && visibleItems.length === 0 && (
        <p style={muted}>No {filter === 'unread' ? 'unread ' : ''}notifications.</p>
      )}
      {state.kind === 'ok' && visibleItems.length > 0 && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          {visibleItems.map(n => (
            <div key={n.id} style={{ ...card, borderLeftColor: n.isRead ? '#e5e7eb' : '#1d4ed8', borderLeftWidth: 4, opacity: n.isRead ? 0.85 : 1 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: n.isRead ? 400 : 600, marginBottom: 4 }}>{n.messageText}</div>
                  <div style={muted}>{new Date(n.createdAt).toLocaleString()} · {n.notificationType}</div>
                  {n.relatedDate && <div style={muted}>Date: {n.relatedDate}{n.relatedTimeSlot ? ` · ${n.relatedTimeSlot}` : ''}</div>}
                </div>
                {!n.isRead && (
                  <button
                    onClick={() => handleMarkRead(n.id)}
                    disabled={busyId === n.id}
                    style={{ ...btn, fontSize: 12, padding: '5px 12px', flexShrink: 0 }}
                  >
                    {busyId === n.id ? '…' : 'Mark read'}
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

const page: React.CSSProperties = { display: 'flex', flexDirection: 'column', gap: 16 };
const card: React.CSSProperties = { background: '#fff', border: '1px solid #e5e7eb', borderRadius: 8, padding: '14px 16px' };
const muted: React.CSSProperties = { color: '#6b7280', fontSize: 13 };
const btn: React.CSSProperties = { background: '#1d4ed8', color: '#fff', border: 'none', borderRadius: 6, padding: '8px 16px', fontSize: 14, fontWeight: 500, cursor: 'pointer' };
