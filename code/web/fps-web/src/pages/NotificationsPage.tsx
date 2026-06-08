import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { fetchNotifications, fetchUnreadCount, markNotificationRead, type NotificationItem } from '../api/notifications';

const TYPE_LABELS: Record<string, string> = {
  'booking.requestSubmitted': 'Request submitted',
  'booking.requestRejected': 'Request rejected',
  'booking.slotAllocated': 'Spot allocated',
  'booking.slotCancelled': 'Spot cancelled',
  'booking.requestCancelled': 'Request cancelled',
};

function typeLabel(notificationType: string): string {
  return TYPE_LABELS[notificationType] ?? notificationType.replace(/^booking\./, '').replace(/([A-Z])/g, ' $1').trim();
}

type State =
  | { kind: 'loading' }
  | { kind: 'ok'; items: NotificationItem[] }
  | { kind: 'error'; message: string };

export function NotificationsPage() {
  const { apiBaseUrl, bearerToken, clear } = useAuth();
  const navigate = useNavigate();
  const [state, setState] = useState<State>({ kind: 'loading' });
  const [unreadCount, setUnreadCount] = useState<number | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'unread'>('all');

  const loadUnreadCount = useCallback(() => {
    fetchUnreadCount({ apiBaseUrl, bearerToken }).then(result => {
      if (result.kind === 'ok') setUnreadCount(result.data.count);
    });
  }, [apiBaseUrl, bearerToken]);

  const load = useCallback(() => {
    setState({ kind: 'loading' });
    fetchNotifications({ apiBaseUrl, bearerToken }).then((result) => {
      if (result.kind === 'unauthenticated') { clear(); navigate('/session'); return; }
      if (result.kind === 'ok') setState({ kind: 'ok', items: result.data.items });
      else setState({ kind: 'error', message: 'message' in result ? result.message : 'Failed to load notifications.' });
    });
    loadUnreadCount();
  }, [apiBaseUrl, bearerToken, clear, navigate, loadUnreadCount]);

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
      setUnreadCount(prev => prev !== null ? Math.max(0, prev - 1) : null);
    }
  }

  const visibleItems = state.kind === 'ok'
    ? (filter === 'unread' ? state.items.filter(n => !n.isRead) : state.items)
    : [];

  return (
    <div className="page-stack">
      <div className="page-hero">
        <div>
          <h2>Notifications{unreadCount !== null && unreadCount > 0 ? ` (${unreadCount} unread)` : ''}</h2>
          <p>Your booking activity and updates</p>
        </div>
      </div>

      <div className="panel">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
          <span style={{ fontSize: 14, color: 'var(--muted)' }}>
            {state.kind === 'ok' ? `${visibleItems.length} ${filter === 'unread' ? 'unread' : 'total'}` : ''}
          </span>
          <div style={{ display: 'flex', gap: 0, border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
            {(['all', 'unread'] as const).map(f => (
              <button key={f} onClick={() => setFilter(f)}
                style={{ background: filter === f ? 'var(--brand-primary)' : '#fff', color: filter === f ? '#fff' : 'var(--muted)', border: 'none', padding: '7px 18px', fontSize: 13, fontWeight: 500, cursor: 'pointer' }}>
                {f === 'all' ? 'All' : 'Unread'}
              </button>
            ))}
          </div>
        </div>

        {state.kind === 'loading' && <p style={{ color: 'var(--muted)', fontSize: 14 }}>Loading…</p>}
        {state.kind === 'error' && (
          <div>
            <p style={{ color: 'var(--danger)', fontSize: 14 }}>{state.message}</p>
            <button onClick={load} className="btn-primary">Retry</button>
          </div>
        )}
        {state.kind === 'ok' && visibleItems.length === 0 && (
          <p style={{ color: 'var(--muted)', fontSize: 14 }}>No {filter === 'unread' ? 'unread ' : ''}notifications.</p>
        )}
        {state.kind === 'ok' && visibleItems.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {visibleItems.map(n => (
              <div key={n.id} style={{ background: '#fff', border: '1px solid var(--border)', borderLeft: `4px solid ${n.isRead ? 'var(--border)' : 'var(--brand-primary)'}`, borderRadius: 8, padding: '14px 16px', opacity: n.isRead ? 0.8 : 1 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: n.isRead ? 400 : 600, marginBottom: 4 }}>{n.messageText}</div>
                    <div style={{ color: 'var(--muted)', fontSize: 13 }}>
                      {new Date(n.createdAt).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
                      {' · '}
                      <span style={{ fontWeight: 500 }}>{typeLabel(n.notificationType)}</span>
                    </div>
                    {n.relatedDate && (
                      <div style={{ color: 'var(--muted)', fontSize: 13 }}>
                        {n.relatedDate}{n.relatedTimeSlot ? ` · ${n.relatedTimeSlot}` : ''}
                      </div>
                    )}
                  </div>
                  {!n.isRead && (
                    <button
                      onClick={() => handleMarkRead(n.id)}
                      disabled={busyId === n.id}
                      className="btn-secondary"
                      style={{ fontSize: 12, padding: '5px 12px', flexShrink: 0 }}
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
    </div>
  );
}
