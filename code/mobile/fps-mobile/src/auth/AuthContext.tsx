import AsyncStorage from '@react-native-async-storage/async-storage';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { fetchMe } from '@/api/client';
import { getOidcConfig } from './oidcConfig';
import { clearAccessToken, loadAccessToken, saveAccessToken, saveForcePromptLogin } from './authStorage';

const DEV_TOKEN_KEY = 'fps.devBearerToken';
const DEV_BASE_URL_KEY = 'fps.apiBaseUrl';

export type AuthState = {
  ready: boolean;
  apiBaseUrl: string;
  bearerToken: string;
  roles: string[];
  isConfigured: boolean;
  setSession: (accessToken: string) => Promise<void>;
  clearSession: () => Promise<void>;
  /** Explicit user sign-out: clears session and marks that the next OIDC sign-in must be interactive. */
  signOut: () => Promise<void>;
  // Development only - preserved for the debug-session screen
  saveCredentials: (apiBaseUrl: string, bearerToken: string) => Promise<void>;
  clearCredentials: () => Promise<void>;
};

const AuthContext = createContext<AuthState | undefined>(undefined);

async function loadRoles(apiBaseUrl: string, bearerToken: string): Promise<string[]> {
  if (!apiBaseUrl || !bearerToken) return [];
  const result = await fetchMe({ apiBaseUrl, bearerToken });
  return result.kind === 'ok' ? (result.me.roles as string[]) : [];
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [apiBaseUrl, setApiBaseUrl] = useState('');
  const [bearerToken, setBearerToken] = useState('');
  const [roles, setRoles] = useState<string[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const oidcToken = await loadAccessToken();
        if (oidcToken) {
          const { apiBaseUrl: configUrl } = getOidcConfig();
          const fetchedRoles = await loadRoles(configUrl, oidcToken);
          if (!cancelled) {
            setApiBaseUrl(configUrl);
            setBearerToken(oidcToken);
            setRoles(fetchedRoles);
          }
          return;
        }
        const [storedBaseUrl, storedToken] = await Promise.all([
          AsyncStorage.getItem(DEV_BASE_URL_KEY),
          AsyncStorage.getItem(DEV_TOKEN_KEY),
        ]);
        if (storedBaseUrl && storedToken) {
          const fetchedRoles = await loadRoles(storedBaseUrl, storedToken);
          if (!cancelled) {
            setApiBaseUrl(storedBaseUrl);
            setBearerToken(storedToken);
            setRoles(fetchedRoles);
          }
        } else if (!cancelled) {
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
    await Promise.all([
      saveAccessToken(accessToken),
      AsyncStorage.removeItem(DEV_BASE_URL_KEY),
      AsyncStorage.removeItem(DEV_TOKEN_KEY),
    ]);
    const { apiBaseUrl: configUrl } = getOidcConfig();
    const fetchedRoles = await loadRoles(configUrl, accessToken);
    setApiBaseUrl(configUrl);
    setBearerToken(accessToken);
    setRoles(fetchedRoles);
  }, []);

  const clearSession = useCallback(async () => {
    await Promise.all([
      clearAccessToken(),
      AsyncStorage.removeItem(DEV_BASE_URL_KEY),
      AsyncStorage.removeItem(DEV_TOKEN_KEY),
    ]);
    setApiBaseUrl('');
    setBearerToken('');
    setRoles([]);
  }, []);

  const signOut = useCallback(async () => {
    await clearSession();
    await saveForcePromptLogin();
  }, [clearSession]);

  const saveCredentials = useCallback(async (nextBaseUrl: string, nextToken: string) => {
    const trimmedBaseUrl = nextBaseUrl.trim().replace(/\/+$/, '');
    const trimmedToken = nextToken.trim();
    await Promise.all([
      clearAccessToken(),
      AsyncStorage.setItem(DEV_BASE_URL_KEY, trimmedBaseUrl),
      AsyncStorage.setItem(DEV_TOKEN_KEY, trimmedToken),
    ]);
    const fetchedRoles = await loadRoles(trimmedBaseUrl, trimmedToken);
    setApiBaseUrl(trimmedBaseUrl);
    setBearerToken(trimmedToken);
    setRoles(fetchedRoles);
  }, []);

  const clearCredentials = useCallback(async () => {
    await Promise.all([
      AsyncStorage.removeItem(DEV_BASE_URL_KEY),
      AsyncStorage.removeItem(DEV_TOKEN_KEY),
    ]);
    setApiBaseUrl('');
    setBearerToken('');
    setRoles([]);
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      ready,
      apiBaseUrl,
      bearerToken,
      roles,
      isConfigured: ready && apiBaseUrl.length > 0 && bearerToken.length > 0,
      setSession,
      clearSession,
      signOut,
      saveCredentials,
      clearCredentials,
    }),
    [ready, apiBaseUrl, bearerToken, roles, setSession, clearSession, signOut, saveCredentials, clearCredentials],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside <AuthProvider>');
  return value;
}
