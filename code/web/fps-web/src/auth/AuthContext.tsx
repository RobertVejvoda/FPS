import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { UserManager } from 'oidc-client-ts';
import { fetchMe } from '../api/client';
import { loadRuntimeConfig, type RuntimeConfig } from './runtimeConfig';
import { clearCredentials, loadBaseUrl, loadToken, saveCredentials } from './authStorage';

export type AuthPhase =
  | 'loading'
  | 'invalid-config'
  | 'unauthenticated'
  | 'login-cancelled'
  | 'login-failed'
  | 'session-expired'
  | 'validating'
  | 'unreachable'
  | 'authenticated';

type AuthState = {
  phase: AuthPhase;
  phaseError: string | undefined;
  apiBaseUrl: string;
  bearerToken: string;
  isConfigured: boolean;
  devFallbackEnabled: boolean;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  save: (apiBaseUrl: string, token: string) => Promise<void>;
  clear: () => void;
};

const AuthContext = createContext<AuthState | undefined>(undefined);

function createUserManager(config: RuntimeConfig): UserManager {
  return new UserManager({
    authority: config.oidc.authority,
    client_id: config.oidc.clientId,
    redirect_uri: config.oidc.redirectUri,
    post_logout_redirect_uri: config.oidc.postLogoutRedirectUri,
    scope: config.oidc.scopes,
    response_type: 'code',
  });
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const userManagerRef = useRef<UserManager | null>(null);
  const configRef = useRef<RuntimeConfig | null>(null);

  const [phase, setPhase] = useState<AuthPhase>('loading');
  const [phaseError, setPhaseError] = useState<string | undefined>(undefined);
  const [apiBaseUrl, setApiBaseUrl] = useState('');
  const [bearerToken, setBearerToken] = useState('');
  const [devFallbackEnabled, setDevFallbackEnabled] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function init() {
      try {
        const config = await loadRuntimeConfig();
        if (cancelled) return;

        configRef.current = config;
        const um = createUserManager(config);
        userManagerRef.current = um;
        setApiBaseUrl(config.apiBaseUrl);
        setDevFallbackEnabled(config.devTokenFallbackEnabled);

        // On the callback page, handle the OIDC redirect inline.
        if (window.location.pathname === '/auth/callback') {
          try {
            const user = await um.signinRedirectCallback();
            if (cancelled) return;
            setPhase('validating');
            const result = await fetchMe({
              apiBaseUrl: config.apiBaseUrl,
              bearerToken: user.access_token,
            });
            if (cancelled) return;
            if (result.kind === 'ok') {
              setBearerToken(user.access_token);
              setPhase('authenticated');
            } else if (result.kind === 'unreachable') {
              setPhaseError(result.message);
              setPhase('unreachable');
            } else {
              setPhase('session-expired');
            }
          } catch (e: unknown) {
            if (!cancelled) {
              const msg = e instanceof Error ? e.message : '';
              setPhase(msg.includes('access_denied') ? 'login-cancelled' : 'login-failed');
            }
          }
          return;
        }

        // Restore dev-fallback token if enabled and stored.
        if (config.devTokenFallbackEnabled) {
          const storedToken = loadToken();
          const storedBase = loadBaseUrl();
          if (storedToken && storedBase) {
            setPhase('validating');
            const result = await fetchMe({ apiBaseUrl: storedBase, bearerToken: storedToken });
            if (cancelled) return;
            if (result.kind === 'ok') {
              setApiBaseUrl(storedBase);
              setBearerToken(storedToken);
              setPhase('authenticated');
              return;
            }
            clearCredentials();
          }
        }

        // Try to restore an existing OIDC session.
        const user = await um.getUser();
        if (cancelled) return;

        if (!user || user.expired) {
          setPhase('unauthenticated');
          return;
        }

        setPhase('validating');
        const result = await fetchMe({
          apiBaseUrl: config.apiBaseUrl,
          bearerToken: user.access_token,
        });
        if (cancelled) return;

        if (result.kind === 'ok') {
          setBearerToken(user.access_token);
          setPhase('authenticated');
        } else if (result.kind === 'unreachable') {
          setPhaseError(result.message);
          setPhase('unreachable');
        } else {
          setPhase('session-expired');
        }
      } catch (e: unknown) {
        if (!cancelled) {
          setPhaseError(e instanceof Error ? e.message : 'Configuration error');
          setPhase('invalid-config');
        }
      }
    }

    void init();
    return () => { cancelled = true; };
  }, []);

  const login = useCallback(async () => {
    const um = userManagerRef.current;
    if (!um) return;
    try {
      await um.signinRedirect();
    } catch {
      setPhase('login-failed');
    }
  }, []);

  const logout = useCallback(async () => {
    clearCredentials();
    setBearerToken('');
    const um = userManagerRef.current;
    if (um) {
      try { await um.removeUser(); } catch { /* best effort */ }
      try {
        await um.signoutRedirect();
      } catch {
        setPhase('unauthenticated');
      }
    } else {
      setPhase('unauthenticated');
    }
  }, []);

  const save = useCallback(async (baseUrl: string, token: string) => {
    const normalizedBase = baseUrl.trim().replace(/\/+$/, '');
    const normalizedToken = token.trim();
    saveCredentials(normalizedBase, normalizedToken);
    setApiBaseUrl(normalizedBase);
    setBearerToken(normalizedToken);
    setPhase('validating');
    const result = await fetchMe({ apiBaseUrl: normalizedBase, bearerToken: normalizedToken });
    if (result.kind === 'ok') {
      setPhase('authenticated');
    } else if (result.kind === 'unreachable') {
      setPhaseError(result.message);
      setPhase('unreachable');
    } else {
      setPhase('session-expired');
    }
  }, []);

  const clear = useCallback(() => {
    clearCredentials();
    setBearerToken('');
    setApiBaseUrl(configRef.current?.apiBaseUrl ?? '');
    setPhase('unauthenticated');
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      phase,
      phaseError,
      apiBaseUrl,
      bearerToken,
      isConfigured: phase === 'authenticated',
      devFallbackEnabled,
      login,
      logout,
      save,
      clear,
    }),
    [phase, phaseError, apiBaseUrl, bearerToken, devFallbackEnabled, login, logout, save, clear],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be inside AuthProvider');
  return value;
}
