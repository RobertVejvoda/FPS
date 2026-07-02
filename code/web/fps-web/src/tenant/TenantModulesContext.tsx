import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useAuth } from '../auth/AuthContext';
import { fetchTenantModules } from '../api/customer';

// PLAT-seats (#710) — which product modules the signed-in tenant runs. The employee UI shows a
// module switch only when more than one module is enabled; a single-module tenant is unchanged.
// Defaults to Parking-only until confirmed (and stays there on any failure), so seat surfaces never
// appear for a tenant we can't confirm has Seats enabled.
type TenantModules = {
  primaryModule: string;
  enabledModules: string[];
  hasSeats: boolean;
  multiModule: boolean;
  loading: boolean;
};

const DEFAULT: TenantModules = {
  primaryModule: 'Parking',
  enabledModules: ['Parking'],
  hasSeats: false,
  multiModule: false,
  loading: true,
};

const TenantModulesContext = createContext<TenantModules>(DEFAULT);

export function TenantModulesProvider({ children }: { children: ReactNode }) {
  const { apiBaseUrl, bearerToken, tenantId, isConfigured } = useAuth();
  const [state, setState] = useState<{ primaryModule: string; enabledModules: string[]; loading: boolean }>({
    primaryModule: 'Parking', enabledModules: ['Parking'], loading: true,
  });

  useEffect(() => {
    // Platform-plane sessions have no tenant; leave the Parking-only default.
    if (!isConfigured || !tenantId) { setState({ primaryModule: 'Parking', enabledModules: ['Parking'], loading: false }); return; }
    let active = true;
    setState((s) => ({ ...s, loading: true }));
    void fetchTenantModules({ apiBaseUrl, bearerToken }, tenantId).then((r) => {
      if (!active) return;
      if (r.kind === 'ok' && r.data.enabledModules.length > 0) {
        setState({ primaryModule: r.data.primaryModule, enabledModules: r.data.enabledModules, loading: false });
      } else {
        // Unknown / failure → stay Parking-only so no seat surfaces appear unexpectedly.
        setState({ primaryModule: 'Parking', enabledModules: ['Parking'], loading: false });
      }
    });
    return () => { active = false; };
  }, [apiBaseUrl, bearerToken, tenantId, isConfigured]);

  const value = useMemo<TenantModules>(() => ({
    primaryModule: state.primaryModule,
    enabledModules: state.enabledModules,
    hasSeats: state.enabledModules.includes('Seats'),
    multiModule: state.enabledModules.length > 1,
    loading: state.loading,
  }), [state]);

  return <TenantModulesContext.Provider value={value}>{children}</TenantModulesContext.Provider>;
}

export function useTenantModules(): TenantModules {
  return useContext(TenantModulesContext);
}
