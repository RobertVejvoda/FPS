import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { fetchMe, type SessionResult } from './client';

export type SessionState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | SessionResult;

// Probes GET /me with the current dev credentials. Returns the same SessionResult
// shape that fetchMe returns, plus an idle/loading phase for the initial render.
// Re-runs whenever credentials change; callers can also refresh on demand.
export function useSession(): {
  state: SessionState;
  refresh: () => void;
} {
  const { ready, apiBaseUrl, bearerToken, isConfigured } = useAuth();
  const [state, setState] = useState<SessionState>({ kind: 'idle' });
  const [refreshKey, setRefreshKey] = useState(0);

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
    setState({ kind: 'loading' });
    fetchMe({ apiBaseUrl, bearerToken }).then((result) => {
      if (!cancelled) setState(result);
    });

    return () => {
      cancelled = true;
    };
  }, [ready, apiBaseUrl, bearerToken, isConfigured, refreshKey]);

  const refresh = useCallback(() => setRefreshKey((k) => k + 1), []);
  return { state, refresh };
}
