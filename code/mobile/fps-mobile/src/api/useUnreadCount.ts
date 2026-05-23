import { useEffect, useState } from 'react';
import { AppState, type AppStateStatus } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { fetchUnreadCount } from './notifications';

const POLL_INTERVAL_MS = 30_000;

export function useUnreadCount(enabled = true): number {
  const { isConfigured, apiBaseUrl, bearerToken } = useAuth();
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (!enabled || !isConfigured) {
      setCount(0);
      return;
    }

    let cancelled = false;

    function poll() {
      fetchUnreadCount({ apiBaseUrl, bearerToken }).then((result) => {
        if (!cancelled && result.kind === 'ok') setCount(result.count);
      });
    }

    poll();
    const id = setInterval(poll, POLL_INTERVAL_MS);

    function handleAppState(next: AppStateStatus) {
      if (next === 'active') poll();
    }
    const sub = AppState.addEventListener('change', handleAppState);

    return () => {
      cancelled = true;
      clearInterval(id);
      sub.remove();
    };
  }, [enabled, isConfigured, apiBaseUrl, bearerToken]);

  return count;
}
