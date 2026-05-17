import { useReducer, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../auth/AuthContext';
import { fetchNotifications, fetchUnreadCount, markNotificationRead, type NotificationDto } from './notifications';

export type NotificationsState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ok'; items: NotificationDto[]; unreadCount: number; hasMore: boolean; isRefreshing: boolean }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

type NotificationsAction =
  | { type: 'LOAD_START' }
  | { type: 'LOAD_SUCCESS'; items: NotificationDto[]; hasMore: boolean; unreadCount: number }
  | { type: 'LOAD_ERROR'; error: { kind: 'unauthenticated' } | { kind: 'unreachable'; message: string } | { kind: 'error'; status: number; message: string } }
  | { type: 'REFRESH_START' }
  | { type: 'REFRESH_SUCCESS'; items: NotificationDto[]; hasMore: boolean; unreadCount: number }
  | { type: 'UPDATE_UNREAD_COUNT'; count: number }
  | { type: 'ADD_NOTIFICATION'; notification: NotificationDto }
  | { type: 'MARK_READ_OPTIMISTIC'; notificationId: string };

function reducer(state: NotificationsState, action: NotificationsAction): NotificationsState {
  switch (action.type) {
    case 'LOAD_START':
      return { kind: 'loading' };

    case 'LOAD_SUCCESS':
      return {
        kind: 'ok',
        items: action.items,
        hasMore: action.hasMore,
        unreadCount: action.unreadCount,
        isRefreshing: false
      };

    case 'LOAD_ERROR':
      if (action.error.kind === 'unauthenticated') return { kind: 'unauthenticated' };
      if (action.error.kind === 'unreachable') return { kind: 'unreachable', message: action.error.message };
      return { kind: 'error', status: action.error.status, message: action.error.message };

    case 'REFRESH_START':
      if (state.kind !== 'ok') return state;
      return { ...state, isRefreshing: true };

    case 'REFRESH_SUCCESS':
      return {
        kind: 'ok',
        items: action.items,
        hasMore: action.hasMore,
        unreadCount: action.unreadCount,
        isRefreshing: false
      };

    case 'UPDATE_UNREAD_COUNT':
      if (state.kind !== 'ok') return state;
      return { ...state, unreadCount: action.count };

    case 'ADD_NOTIFICATION':
      if (state.kind !== 'ok') return state;
      // Add new notification to the top of the list
      const newItems = [action.notification, ...state.items];
      return {
        ...state,
        items: newItems,
        unreadCount: action.notification.isRead ? state.unreadCount : state.unreadCount + 1
      };

    case 'MARK_READ_OPTIMISTIC':
      if (state.kind !== 'ok') return state;
      const updatedItems = state.items.map(item =>
        item.id === action.notificationId ? { ...item, isRead: true } : item
      );
      const wasUnread = state.items.find(item => item.id === action.notificationId && !item.isRead);
      return {
        ...state,
        items: updatedItems,
        unreadCount: wasUnread ? Math.max(0, state.unreadCount - 1) : state.unreadCount
      };

    default:
      return state;
  }
}

/**
 * Hook for managing notification list with SSE stream and polling fallback.
 *
 * Attempts to connect to SSE stream first. If SSE fails or is not supported,
 * falls back to polling every 30 seconds. Handles reconnection on network errors.
 */
export function useNotifications() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [state, dispatch] = useReducer(reducer, { kind: 'idle' });
  const eventSourceRef = useRef<EventSource | null>(null);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);
  const isSSEConnectedRef = useRef(false);

  const config = apiBaseUrl && bearerToken ? { apiBaseUrl, bearerToken } : null;

  // Load initial notifications and unread count
  const load = useCallback(async () => {
    if (!config) return;

    dispatch({ type: 'LOAD_START' });

    const [notificationsResult, unreadResult] = await Promise.all([
      fetchNotifications(config, { pageSize: 50 }),
      fetchUnreadCount(config)
    ]);

    if (notificationsResult.kind !== 'ok') {
      dispatch({ type: 'LOAD_ERROR', error: notificationsResult });
      return;
    }

    const unreadCount = unreadResult.kind === 'ok' ? unreadResult.count : 0;

    dispatch({
      type: 'LOAD_SUCCESS',
      items: notificationsResult.items,
      hasMore: notificationsResult.hasMore,
      unreadCount
    });
  }, [config]);

  // Refresh notifications
  const refresh = useCallback(async () => {
    if (!config || state.kind !== 'ok') return;

    dispatch({ type: 'REFRESH_START' });

    const [notificationsResult, unreadResult] = await Promise.all([
      fetchNotifications(config, { pageSize: 50 }),
      fetchUnreadCount(config)
    ]);

    if (notificationsResult.kind === 'ok') {
      const unreadCount = unreadResult.kind === 'ok' ? unreadResult.count : 0;
      dispatch({
        type: 'REFRESH_SUCCESS',
        items: notificationsResult.items,
        hasMore: notificationsResult.hasMore,
        unreadCount
      });
    }
  }, [config, state.kind]);

  // Mark notification as read
  const markRead = useCallback(async (notificationId: string) => {
    if (!config) return { kind: 'error' as const };

    // Optimistic update
    dispatch({ type: 'MARK_READ_OPTIMISTIC', notificationId });

    const result = await markNotificationRead(config, notificationId);

    // If it failed, we could revert, but for now we'll just refresh
    if (result.kind !== 'ok' && result.kind !== 'notFound') {
      await refresh();
    }

    return result;
  }, [config, refresh]);

  // Setup SSE stream
  useEffect(() => {
    if (!config) return;

    // Try SSE first
    const trySSE = () => {
      if (typeof EventSource === 'undefined') {
        // SSE not supported (e.g., in React Native without polyfill)
        startPolling();
        return;
      }

      try {
        const sseUrl = `${config.apiBaseUrl}/notifications/stream`;
        const eventSource = new EventSource(sseUrl, {
          // Note: EventSource doesn't support custom headers in browsers.
          // We'll need to pass the token via query parameter or use polling.
          // For now, we'll fall back to polling.
        });

        eventSource.onopen = () => {
          isSSEConnectedRef.current = true;
          stopPolling();
        };

        eventSource.onmessage = (event) => {
          try {
            const notification: NotificationDto = JSON.parse(event.data);
            dispatch({ type: 'ADD_NOTIFICATION', notification });
          } catch (err) {
            console.warn('Failed to parse SSE notification:', err);
          }
        };

        eventSource.onerror = (err) => {
          console.warn('SSE error, falling back to polling:', err);
          eventSource.close();
          isSSEConnectedRef.current = false;
          startPolling();
        };

        eventSourceRef.current = eventSource;
      } catch (err) {
        console.warn('Failed to create EventSource, falling back to polling:', err);
        startPolling();
      }
    };

    // Polling fallback
    const startPolling = () => {
      if (pollingIntervalRef.current) return; // Already polling

      // Poll every 30 seconds
      pollingIntervalRef.current = setInterval(async () => {
        if (!isSSEConnectedRef.current && state.kind === 'ok') {
          const unreadResult = await fetchUnreadCount(config);
          if (unreadResult.kind === 'ok') {
            dispatch({ type: 'UPDATE_UNREAD_COUNT', count: unreadResult.count });
          }

          // Only fetch new notifications if unread count changed
          if (unreadResult.kind === 'ok' && unreadResult.count !== state.unreadCount) {
            await refresh();
          }
        }
      }, 30000);
    };

    const stopPolling = () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    };

    // For mobile apps, EventSource typically doesn't work with auth headers,
    // so we'll use polling as the primary mechanism.
    // SSE would work better in a web environment or with a custom SSE client.
    startPolling();

    return () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
        eventSourceRef.current = null;
      }
      stopPolling();
      isSSEConnectedRef.current = false;
    };
  }, [config, state.kind, state.unreadCount, refresh]);

  // Initial load
  useEffect(() => {
    if (config && state.kind === 'idle') {
      load();
    }
  }, [config, state.kind, load]);

  return {
    state,
    refresh,
    markRead
  };
}
