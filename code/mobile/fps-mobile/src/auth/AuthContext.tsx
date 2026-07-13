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
  /** Server-confirmed tenant from GET /me — used only for tenant-scoped read URLs
   *  such as the module list (UX009 #782); never sent as a scoping claim. */
  tenantId: string;
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

async function loadIdentity(apiBaseUrl: string, bearerToken: string): Promise<{ roles: string[]; tenantId: string }> {
  if (!apiBaseUrl || !bearerToken) return { roles: [], tenantId: '' };
  const result = await fetchMe({ apiBaseUrl, bearerToken });
  return result.kind === 'ok'
    ? { roles: result.me.roles as string[], tenantId: (result.me.tenantId as string) ?? '' }
    : { roles: [], tenantId: '' };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [apiBaseUrl, setApiBaseUrl] = useState('');
  const [bearerToken, setBearerToken] = useState('');
  const [roles, setRoles] = useState<string[]>([]);
  const [tenantId, setTenantId] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const oidcToken = await loadAccessToken();
        if (oidcToken) {
          const { apiBaseUrl: configUrl } = getOidcConfig();
          const identity = await loadIdentity(configUrl, oidcToken);
          if (!cancelled) {
            setApiBaseUrl(configUrl);
            setBearerToken(oidcToken);
            setRoles(identity.roles);
            setTenantId(identity.tenantId);
          }
          return;
        }
        const [storedBaseUrl, storedToken] = await Promise.all([
          AsyncStorage.getItem(DEV_BASE_URL_KEY),
          AsyncStorage.getItem(DEV_TOKEN_KEY),
        ]);
        if (storedBaseUrl && storedToken) {
          const identity = await loadIdentity(storedBaseUrl, storedToken);
          if (!cancelled) {
            setApiBaseUrl(storedBaseUrl);
            setBearerToken(storedToken);
            setRoles(identity.roles);
            setTenantId(identity.tenantId);
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
    const identity = await loadIdentity(configUrl, accessToken);
    setApiBaseUrl(configUrl);
    setBearerToken(accessToken);
    setRoles(identity.roles);
    setTenantId(identity.tenantId);
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
    setTenantId('');
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
    const identity = await loadIdentity(trimmedBaseUrl, trimmedToken);
    setApiBaseUrl(trimmedBaseUrl);
    setBearerToken(trimmedToken);
    setRoles(identity.roles);
    setTenantId(identity.tenantId);
  }, []);

  const clearCredentials = useCallback(async () => {
    await Promise.all([
      AsyncStorage.removeItem(DEV_BASE_URL_KEY),
      AsyncStorage.removeItem(DEV_TOKEN_KEY),
    ]);
    setApiBaseUrl('');
    setBearerToken('');
    setRoles([]);
    setTenantId('');
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      ready,
      apiBaseUrl,
      bearerToken,
      roles,
      tenantId,
      isConfigured: ready && apiBaseUrl.length > 0 && bearerToken.length > 0,
      setSession,
      clearSession,
      signOut,
      saveCredentials,
      clearCredentials,
    }),
    [ready, apiBaseUrl, bearerToken, roles, tenantId, setSession, clearSession, signOut, saveCredentials, clearCredentials],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside <AuthProvider>');
  return value;
}
