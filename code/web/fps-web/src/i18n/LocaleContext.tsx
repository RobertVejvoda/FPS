// LOC001 (#744) — locale resolution and React wiring.
//
// Resolution follows the PO-defined precedence (issue #744): explicit user
// preference (?lang= for testing, then the stored choice) → authenticated
// tenant default locale → deployment/runtime config default → browser
// Accept-Language hint → English. IP geolocation is deliberately not a
// source. Switching UI language is a presentation preference only; it never
// touches tenant market/payment configuration.
import {
  Fragment,
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react';
import {
  LOCALE_STORAGE_KEY,
  SUPPORTED_LOCALES,
  getCurrentLocale,
  setCurrentLocale,
  subscribeToLocale,
  toSupportedLocale,
  type Locale,
} from './locale';
import { useAuth } from '../auth/AuthContext';
import { fetchTenantModules } from '../api/customer';

type LocaleContextValue = {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  // Feeds a pre-auth tenant default (e.g. from sign-in email discovery) into
  // the tenant-default precedence slot. Transient: never persisted, never
  // overrides an explicit user preference or the ?lang test override.
  applyTenantDefault: (tag: string | null | undefined) => void;
};

const LocaleContext = createContext<LocaleContextValue | undefined>(undefined);

function readStoredPreference(): Locale | null {
  try {
    return toSupportedLocale(window.localStorage.getItem(LOCALE_STORAGE_KEY));
  } catch {
    return null;
  }
}

function browserHint(): Locale | null {
  for (const tag of navigator.languages ?? [navigator.language]) {
    const supported = toSupportedLocale(tag);
    if (supported) return supported;
  }
  return null;
}

export function LocaleProvider({ children }: { children: ReactNode }) {
  const { defaultLocale: configDefault, apiBaseUrl, bearerToken, tenantId, isConfigured } = useAuth();
  const [userPreference, setUserPreference] = useState<Locale | null>(readStoredPreference);
  const [tenantDefault, setTenantDefault] = useState<Locale | null>(null);

  // ?lang=cs|en — session-only override for testing both languages locally
  // without persisting a preference (PO clarification on #744).
  const queryOverride = useMemo(
    () => toSupportedLocale(new URLSearchParams(window.location.search).get('lang')),
    [],
  );
  const hint = useMemo(browserHint, []);

  // Tenant default locale arrives with the tenant modules bootstrap once the
  // session is authenticated. Failures leave the lower-precedence sources in
  // charge — never block rendering on this fetch.
  useEffect(() => {
    if (!isConfigured || !tenantId) {
      setTenantDefault(null);
      return;
    }
    let active = true;
    void fetchTenantModules({ apiBaseUrl, bearerToken }, tenantId).then((r) => {
      if (!active) return;
      if (r.kind === 'ok') setTenantDefault(toSupportedLocale(r.data.defaultLocale));
    });
    return () => { active = false; };
  }, [isConfigured, tenantId, apiBaseUrl, bearerToken]);

  const effective: Locale =
    queryOverride ??
    userPreference ??
    tenantDefault ??
    toSupportedLocale(configDefault) ??
    hint ??
    'en';

  useEffect(() => {
    setCurrentLocale(effective);
    document.documentElement.lang = effective;
  }, [effective]);

  const locale = useSyncExternalStore(subscribeToLocale, getCurrentLocale);

  const setLocale = useCallback((next: Locale) => {
    try {
      window.localStorage.setItem(LOCALE_STORAGE_KEY, next);
    } catch {
      // Storage may be unavailable (private mode); the choice still applies
      // for this session.
    }
    setUserPreference(next);
    setCurrentLocale(next);
  }, []);

  // LOC001 review (#802): the sign-in discovery flow calls this right before
  // redirecting to Keycloak, so the store must update synchronously — the
  // render effect would apply one tick too late for login()'s ui_locales.
  const applyTenantDefault = useCallback((tag: string | null | undefined) => {
    const supported = toSupportedLocale(tag);
    setTenantDefault(supported);
    if (supported && !queryOverride && !userPreference) setCurrentLocale(supported);
  }, [queryOverride, userPreference]);

  const value = useMemo<LocaleContextValue>(
    () => ({ locale, setLocale, applyTenantDefault }),
    [locale, setLocale, applyTenantDefault],
  );

  return (
    <LocaleContext.Provider value={value}>
      {/* Remount the subtree on locale change so module-level helpers
          (displayLabels, formatters) re-evaluate everywhere. Language
          switches are rare, so the remount cost is acceptable. */}
      <Fragment key={locale}>{children}</Fragment>
    </LocaleContext.Provider>
  );
}

export function useLocale(): LocaleContextValue {
  const value = useContext(LocaleContext);
  if (!value) throw new Error('useLocale must be inside LocaleProvider');
  return value;
}

export { SUPPORTED_LOCALES };
export type { Locale };
