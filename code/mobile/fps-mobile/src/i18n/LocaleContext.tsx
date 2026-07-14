// LOC001 (#744) — locale resolution and React wiring for the mobile app.
//
// Resolution precedence (mobile): stored user preference (AsyncStorage) →
// device locale hint (Intl.DateTimeFormat().resolvedOptions().locale) →
// English. There is no ?lang= override (no query string on native) and no
// tenant-default-locale fetch yet — the web LocaleContext fetches the
// tenant's defaultLocale via fetchTenantModules once authenticated; wiring
// that up for mobile is a documented follow-up, not built here. Switching UI
// language is a presentation preference only; it never touches tenant
// market/payment configuration.
//
// AsyncStorage is async, so the initial locale is resolved before the first
// render of `children` — a tiny gate renders null until that resolves,
// mirroring how AuthProvider gates on `ready` while it restores the stored
// session (see src/auth/AuthContext.tsx).
import AsyncStorage from '@react-native-async-storage/async-storage';
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

type LocaleContextValue = {
  locale: Locale;
  setLocale: (locale: Locale) => void;
};

const LocaleContext = createContext<LocaleContextValue | undefined>(undefined);

async function readStoredPreference(): Promise<Locale | null> {
  try {
    const stored = await AsyncStorage.getItem(LOCALE_STORAGE_KEY);
    return toSupportedLocale(stored);
  } catch {
    return null;
  }
}

// Device locale hint. Intl is preferred over adding the expo-localization
// package — Hermes on RN 0.81 supports Intl.DateTimeFormat().resolvedOptions()
// — per the LOC001 plan; fall back to expo-localization only if this proves
// unreliable on-device.
function deviceHint(): Locale | null {
  try {
    const tag = Intl.DateTimeFormat().resolvedOptions().locale;
    return toSupportedLocale(tag);
  } catch {
    return null;
  }
}

export function LocaleProvider({ children }: { children: ReactNode }) {
  const [resolved, setResolved] = useState(false);
  const [userPreference, setUserPreference] = useState<Locale | null>(null);

  useEffect(() => {
    let active = true;
    void readStoredPreference().then((stored) => {
      if (!active) return;
      setUserPreference(stored);
      setResolved(true);
    });
    return () => { active = false; };
  }, []);

  const hint = useMemo(deviceHint, []);
  const effective: Locale = userPreference ?? hint ?? 'en';

  useEffect(() => {
    if (resolved) setCurrentLocale(effective);
  }, [resolved, effective]);

  const locale = useSyncExternalStore(subscribeToLocale, getCurrentLocale);

  const setLocale = useCallback((next: Locale) => {
    setUserPreference(next);
    setCurrentLocale(next);
    AsyncStorage.setItem(LOCALE_STORAGE_KEY, next).catch(() => {
      // Storage may be unavailable; the choice still applies for this session.
    });
  }, []);

  const value = useMemo<LocaleContextValue>(() => ({ locale, setLocale }), [locale, setLocale]);

  // Render nothing until the stored preference has loaded — avoids a flash
  // of the wrong language before AsyncStorage resolves.
  if (!resolved) return null;

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
