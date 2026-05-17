import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { fetchProfileSnapshot, type ProfileSnapshot } from './profile';

export type ProfileSnapshotState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ok'; profile: ProfileSnapshot; isRefreshing: boolean }
  | { kind: 'notFound' }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export function useProfileSnapshot(): {
  state: ProfileSnapshotState;
  refresh: () => void;
} {
  const { ready, apiBaseUrl, bearerToken, isConfigured } = useAuth();
  const [state, setState] = useState<ProfileSnapshotState>({ kind: 'idle' });
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
    setState((prev) =>
      prev.kind === 'ok' ? { ...prev, isRefreshing: true } : { kind: 'loading' },
    );

    fetchProfileSnapshot({ apiBaseUrl, bearerToken }).then((result) => {
      if (cancelled) return;
      if (result.kind === 'ok') {
        setState({ kind: 'ok', profile: result.profile, isRefreshing: false });
      } else {
        setState((prev) =>
          prev.kind === 'ok' && result.kind !== 'notFound'
            ? { ...prev, isRefreshing: false }
            : result,
        );
      }
    });

    return () => { cancelled = true; };
  }, [ready, isConfigured, apiBaseUrl, bearerToken, refreshKey]);

  const refresh = useCallback(() => setRefreshKey((k) => k + 1), []);

  return { state, refresh };
}
