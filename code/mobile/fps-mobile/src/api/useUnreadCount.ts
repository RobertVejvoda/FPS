import { useState, useEffect } from 'react';
import { useAuth } from '../auth/AuthContext';
import { fetchUnreadCount } from './notifications';

/**
 * Lightweight hook for fetching only the unread count.
 * Used for the tab badge without loading full notification list.
 */
export function useUnreadCount() {
  const { apiBaseUrl, bearerToken } = useAuth();
  const [count, setCount] = useState<number>(0);

  const config = apiBaseUrl && bearerToken ? { apiBaseUrl, bearerToken } : null;

  useEffect(() => {
    if (!config) return;

    let cancelled = false;

    const fetchCount = async () => {
      const result = await fetchUnreadCount(config);
      if (!cancelled && result.kind === 'ok') {
        setCount(result.count);
      }
    };

    // Initial fetch
    fetchCount();

    // Poll every 30 seconds
    const interval = setInterval(fetchCount, 30000);

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [config]);

  return count;
}
