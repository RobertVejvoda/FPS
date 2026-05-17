import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, type AppStateStatus } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import {
  fetchNotifications,
  markNotificationRead,
  type NotificationItem,
} from './notifications';

const POLL_INTERVAL_MS = 30_000;

export type NotificationsState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ok'; items: NotificationItem[]; isRefreshing: boolean }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export function useNotifications(unreadOnly = false): {
  state: NotificationsState;
  refresh: () => void;
  markRead: (id: string) => Promise<void>;
} {
  const { ready, apiBaseUrl, bearerToken, isConfigured } = useAuth();
  const [state, setState] = useState<NotificationsState>({ kind: 'idle' });
  const [refreshKey, setRefreshKey] = useState(0);
  const unreadOnlyRef = useRef(unreadOnly);
  unreadOnlyRef.current = unreadOnly;

  const load = useCallback(
    (isRefresh: boolean) => {
      if (!isConfigured) {
        setState({ kind: 'unauthenticated' });
        return () => {};
      }
      let cancelled = false;
      setState((prev) =>
        isRefresh && prev.kind === 'ok' ? { ...prev, isRefreshing: true } : { kind: 'loading' },
      );
      fetchNotifications({ apiBaseUrl, bearerToken }, { unreadOnly: unreadOnlyRef.current, pageSize: 50 }).then(
        (result) => {
          if (cancelled) return;
          if (result.kind === 'ok') {
            setState({ kind: 'ok', items: result.items, isRefreshing: false });
          } else {
            setState((prev) => (isRefresh && prev.kind === 'ok' ? { ...prev, isRefreshing: false } : result));
          }
        },
      );
      return () => { cancelled = true; };
    },
    [apiBaseUrl, bearerToken, isConfigured],
  );

  // Initial load and refresh
  useEffect(() => {
    if (!ready) {
      setState({ kind: 'idle' });
      return;
    }
    return load(refreshKey > 0);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ready, isConfigured, refreshKey, load]);

  // Poll every 30 seconds
  useEffect(() => {
    if (!isConfigured) return;
    const id = setInterval(() => setRefreshKey((k) => k + 1), POLL_INTERVAL_MS);
    return () => clearInterval(id);
  }, [isConfigured]);

  // Refresh when app returns to foreground
  useEffect(() => {
    function handleAppState(next: AppStateStatus) {
      if (next === 'active') setRefreshKey((k) => k + 1);
    }
    const sub = AppState.addEventListener('change', handleAppState);
    return () => sub.remove();
  }, []);

  const refresh = useCallback(() => setRefreshKey((k) => k + 1), []);

  const markRead = useCallback(
    async (id: string) => {
      // Optimistic local update
      setState((prev) => {
        if (prev.kind !== 'ok') return prev;
        return {
          ...prev,
          items: prev.items.map((item) => (item.id === id ? { ...item, isRead: true } : item)),
        };
      });
      await markNotificationRead({ apiBaseUrl, bearerToken }, id);
      // Sync with server
      setRefreshKey((k) => k + 1);
    },
    [apiBaseUrl, bearerToken],
  );

  return { state, refresh, markRead };
}
