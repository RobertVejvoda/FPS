import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

// MOB001 keeps dev credentials in AsyncStorage only — never bundled, never persisted server-side.
// Production auth (login, refresh, secure storage) belongs to a later slice.
const STORAGE_KEY_TOKEN = 'fps.devBearerToken';
const STORAGE_KEY_BASE_URL = 'fps.apiBaseUrl';

export type AuthState = {
  ready: boolean;
  apiBaseUrl: string;
  bearerToken: string;
  isConfigured: boolean;
  saveCredentials: (apiBaseUrl: string, bearerToken: string) => Promise<void>;
  clearCredentials: () => Promise<void>;
};

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [apiBaseUrl, setApiBaseUrl] = useState('');
  const [bearerToken, setBearerToken] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [storedBaseUrl, storedToken] = await Promise.all([
          AsyncStorage.getItem(STORAGE_KEY_BASE_URL),
          AsyncStorage.getItem(STORAGE_KEY_TOKEN),
        ]);
        if (cancelled) return;
        if (storedBaseUrl) setApiBaseUrl(storedBaseUrl);
        if (storedToken) setBearerToken(storedToken);
      } finally {
        if (!cancelled) setReady(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      ready,
      apiBaseUrl,
      bearerToken,
      isConfigured: ready && apiBaseUrl.length > 0 && bearerToken.length > 0,
      async saveCredentials(nextBaseUrl, nextToken) {
        const trimmedBaseUrl = nextBaseUrl.trim().replace(/\/+$/, '');
        const trimmedToken = nextToken.trim();
        await Promise.all([
          AsyncStorage.setItem(STORAGE_KEY_BASE_URL, trimmedBaseUrl),
          AsyncStorage.setItem(STORAGE_KEY_TOKEN, trimmedToken),
        ]);
        setApiBaseUrl(trimmedBaseUrl);
        setBearerToken(trimmedToken);
      },
      async clearCredentials() {
        await Promise.all([
          AsyncStorage.removeItem(STORAGE_KEY_BASE_URL),
          AsyncStorage.removeItem(STORAGE_KEY_TOKEN),
        ]);
        setApiBaseUrl('');
        setBearerToken('');
      },
    }),
    [ready, apiBaseUrl, bearerToken],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth must be used inside <AuthProvider>');
  }
  return value;
}
