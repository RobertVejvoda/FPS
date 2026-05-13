import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { fetchBookings, type BookingListItem } from './bookings';

export type BookingsState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingListItem[]; nextCursor: string | null; loadingMore: boolean; isRefreshing: boolean }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export function useBookings(): {
  state: BookingsState;
  refresh: () => void;
  loadMore: () => void;
} {
  const { ready, apiBaseUrl, bearerToken, isConfigured } = useAuth();
  const [state, setState] = useState<BookingsState>({ kind: 'idle' });
  const [refreshKey, setRefreshKey] = useState(0);
  const [loadMoreCursor, setLoadMoreCursor] = useState<string | null>(null);

  // Initial load and refresh
  useEffect(() => {
    if (!ready) {
      setState({ kind: 'idle' });
      return;
    }
    if (!isConfigured) {
      setState({ kind: 'unauthenticated' });
      return;
    }

    let cancelled = false;
    // Keep existing items visible during pull-to-refresh; show spinner on initial load
    setState((prev) =>
      prev.kind === 'ok'
        ? { ...prev, isRefreshing: true }
        : { kind: 'loading' },
    );

    fetchBookings({ apiBaseUrl, bearerToken }).then((result) => {
      if (cancelled) return;
      if (result.kind === 'ok') {
        setState({ kind: 'ok', items: result.items, nextCursor: result.nextCursor, loadingMore: false, isRefreshing: false });
      } else {
        // On refresh failure with existing data, keep the list and clear the refreshing flag
        setState((prev) => (prev.kind === 'ok' ? { ...prev, isRefreshing: false } : result));
      }
    });

    return () => {
      cancelled = true;
    };
  }, [ready, apiBaseUrl, bearerToken, isConfigured, refreshKey]);

  // Cursor pagination
  useEffect(() => {
    if (!loadMoreCursor || !isConfigured) return;

    let cancelled = false;
    fetchBookings({ apiBaseUrl, bearerToken }, loadMoreCursor).then((result) => {
      if (cancelled) return;
      setLoadMoreCursor(null);
      setState((prev) => {
        if (prev.kind !== 'ok') return prev;
        if (result.kind === 'ok') {
          return {
            kind: 'ok',
            items: [...prev.items, ...result.items],
            nextCursor: result.nextCursor,
            loadingMore: false,
            isRefreshing: false,
          };
        }
        return { ...prev, loadingMore: false };
      });
    });

    return () => {
      cancelled = true;
    };
  }, [loadMoreCursor, apiBaseUrl, bearerToken, isConfigured]);

  const refresh = useCallback(() => {
    setLoadMoreCursor(null);
    setRefreshKey((k) => k + 1);
  }, []);

  const loadMore = useCallback(() => {
    setState((prev) => {
      if (prev.kind !== 'ok' || !prev.nextCursor || prev.loadingMore) return prev;
      setLoadMoreCursor(prev.nextCursor);
      return { ...prev, loadingMore: true };
    });
  }, []);

  return { state, refresh, loadMore };
}
