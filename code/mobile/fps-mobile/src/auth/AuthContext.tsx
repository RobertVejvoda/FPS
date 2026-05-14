import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { getOidcConfig } from './oidcConfig';
import { clearAccessToken, loadAccessToken, saveAccessToken } from './authStorage';

const DEV_TOKEN_KEY = 'fps.devBearerToken';
const DEV_BASE_URL_KEY = 'fps.apiBaseUrl';

export type AuthState = {
  ready: boolean;
  apiBaseUrl: string;
  bearerToken: string;
  isConfigured: boolean;
  setSession: (accessToken: string) => Promise<void>;
  clearSession: () => Promise<void>;
  // Development only - preserved for the debug-session screen
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
        const oidcToken = await loadAccessToken();
        if (oidcToken) {
          const { apiBaseUrl: configUrl } = getOidcConfig();
          if (!cancelled) {
            setApiBaseUrl(configUrl);
            setBearerToken(oidcToken);
          }
          return;
        }
        const [storedBaseUrl, storedToken] = await Promise.all([
          AsyncStorage.getItem(DEV_BASE_URL_KEY),
          AsyncStorage.getItem(DEV_TOKEN_KEY),
        ]);
        if (!cancelled) {
          if (storedBaseUrl) setApiBaseUrl(storedBaseUrl);
          if (storedToken) setBearerToken(storedToken);
        }
      } finally {
        if (!cancelled) setReady(true);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const setSession = useCallback(async (accessToken: string) => {
    await saveAccessToken(accessToken);
    const { apiBaseUrl: configUrl } = getOidcConfig();
    setApiBaseUrl(configUrl);
    setBearerToken(accessToken);
  }, []);

  const clearSession = useCallback(async () => {
    await clearAccessToken();
    setApiBaseUrl('');
    setBearerToken('');
  }, []);

  const saveCredentials = useCallback(async (nextBaseUrl: string, nextToken: string) => {
    const trimmedBaseUrl = nextBaseUrl.trim().replace(/\/+$/, '');
    const trimmedToken = nextToken.trim();
    await Promise.all([
      AsyncStorage.setItem(DEV_BASE_URL_KEY, trimmedBaseUrl),
      AsyncStorage.setItem(DEV_TOKEN_KEY, trimmedToken),
    ]);
    setApiBaseUrl(trimmedBaseUrl);
    setBearerToken(trimmedToken);
  }, []);

  const clearCredentials = useCallback(async () => {
    await Promise.all([
      AsyncStorage.removeItem(DEV_BASE_URL_KEY),
      AsyncStorage.removeItem(DEV_TOKEN_KEY),
    ]);
    setApiBaseUrl('');
    setBearerToken('');
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      ready,
      apiBaseUrl,
      bearerToken,
      isConfigured: ready && apiBaseUrl.length > 0 && bearerToken.length > 0,
      setSession,
      clearSession,
      saveCredentials,
      clearCredentials,
    }),
    [ready, apiBaseUrl, bearerToken, setSession, clearSession, saveCredentials, clearCredentials],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside <AuthProvider>');
  return value;
}
