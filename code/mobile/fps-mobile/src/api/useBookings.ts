import { useCallback, useEffect, useRef, useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { fetchBookings, type BookingListItem } from './bookings';

export type BookingsState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ok'; items: BookingListItem[]; nextCursor: string | null; loadingMore: boolean; isRefreshing: boolean }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

function localDateStr(offsetDays = 0): string {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function useBookings(filter: 'upcoming' | 'recent' = 'upcoming'): {
  state: BookingsState;
  refresh: () => void;
  loadMore: () => void;
} {
  const { ready, apiBaseUrl, bearerToken, isConfigured } = useAuth();
  const [state, setState] = useState<BookingsState>({ kind: 'idle' });
  const [refreshKey, setRefreshKey] = useState(0);
  const [loadMoreCursor, setLoadMoreCursor] = useState<string | null>(null);
  const filterRef = useRef(filter);

  // Initial load, refresh, and filter switch
  useEffect(() => {
    if (!ready) {
      setState({ kind: 'idle' });
      return;
    }
    if (!isConfigured) {
      setState({ kind: 'unauthenticated' });
      return;
    }

    const isFilterChange = filterRef.current !== filter;
    filterRef.current = filter;
    if (isFilterChange) setLoadMoreCursor(null);

    let cancelled = false;
    setState((prev) =>
      !isFilterChange && prev.kind === 'ok'
        ? { ...prev, isRefreshing: true }
        : { kind: 'loading' },
    );

    const opts = filter === 'upcoming'
      ? { from: localDateStr(0) }
      : { to: localDateStr(-1) };

    fetchBookings({ apiBaseUrl, bearerToken }, opts).then((result) => {
      if (cancelled) return;
      if (result.kind === 'ok') {
        setState({ kind: 'ok', items: result.items, nextCursor: result.nextCursor, loadingMore: false, isRefreshing: false });
      } else {
        setState((prev) => (prev.kind === 'ok' ? { ...prev, isRefreshing: false } : result));
      }
    });

    return () => {
      cancelled = true;
    };
  }, [ready, apiBaseUrl, bearerToken, isConfigured, refreshKey, filter]);

  // Cursor pagination
  useEffect(() => {
    if (!loadMoreCursor || !isConfigured) return;

    let cancelled = false;
    fetchBookings({ apiBaseUrl, bearerToken }, { cursor: loadMoreCursor }).then((result) => {
      if (cancelled) return;
      setLoadMoreCursor(null);
      setState((prev) => {
        if (prev.kind !== 'ok') return prev;
        if (result.kind === 'ok') {
          const existingIds = new Set(prev.items.map((i) => i.requestId));
          const newItems = result.items.filter((i) => !existingIds.has(i.requestId));
          return {
            kind: 'ok',
            items: [...prev.items, ...newItems],
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
