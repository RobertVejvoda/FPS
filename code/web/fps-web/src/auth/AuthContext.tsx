import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { loadBaseUrl, loadToken, saveCredentials, clearCredentials } from './authStorage';

type AuthState = {
  ready: boolean;
  apiBaseUrl: string;
  bearerToken: string;
  isConfigured: boolean;
  save: (apiBaseUrl: string, bearerToken: string) => void;
  clear: () => void;
};

const AuthContext = createContext<AuthState | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [apiBaseUrl, setApiBaseUrl] = useState('');
  const [bearerToken, setBearerToken] = useState('');

  useEffect(() => {
    setApiBaseUrl(loadBaseUrl());
    setBearerToken(loadToken());
    setReady(true);
  }, []);

  const save = useCallback((url: string, token: string) => {
    saveCredentials(url, token);
    setApiBaseUrl(url.trim().replace(/\/+$/, ''));
    setBearerToken(token.trim());
  }, []);

  const clear = useCallback(() => {
    clearCredentials();
    setApiBaseUrl('');
    setBearerToken('');
  }, []);

  const value = useMemo<AuthState>(
    () => ({
      ready,
      apiBaseUrl,
      bearerToken,
      isConfigured: ready && apiBaseUrl.length > 0 && bearerToken.length > 0,
      save,
      clear,
    }),
    [ready, apiBaseUrl, bearerToken, save, clear],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be inside AuthProvider');
  return value;
}
