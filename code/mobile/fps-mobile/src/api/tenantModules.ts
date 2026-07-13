import { useEffect, useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import type { ApiClientConfig } from './client';

// UX009 (#782) — which product modules the signed-in tenant runs, mirroring the web
// TenantModulesContext rules: default to Parking-only until confirmed, and stay there
// on any failure so seat surfaces never appear for a tenant we can't confirm has
// Seats enabled. The tenantId comes from the server's own GET /me response; it is
// only used to build this read URL, never sent as a scoping claim.
export interface TenantModulesResponse {
  primaryModule: string;
  enabledModules: string[];
}

export type TenantModulesResult =
  | { kind: 'ok'; data: TenantModulesResponse }
  | { kind: 'unauthenticated' }
  | { kind: 'unreachable'; message: string }
  | { kind: 'error'; status: number; message: string };

export async function fetchTenantModules(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
  tenantId: string,
): Promise<TenantModulesResult> {
  if (!apiBaseUrl || !bearerToken || !tenantId) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/tenants/${encodeURIComponent(tenantId)}/modules`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /tenants/{tenantId}/modules returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as TenantModulesResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export type TenantModules = {
  enabledModules: string[];
  hasSeats: boolean;
  loading: boolean;
};

export function useTenantModules(): TenantModules {
  const { ready, apiBaseUrl, bearerToken, tenantId, isConfigured } = useAuth();
  const [state, setState] = useState<TenantModules>({ enabledModules: ['Parking'], hasSeats: false, loading: true });

  useEffect(() => {
    if (!ready) return;
    if (!isConfigured || !tenantId) {
      setState({ enabledModules: ['Parking'], hasSeats: false, loading: false });
      return;
    }
    let cancelled = false;
    setState((s) => ({ ...s, loading: true }));
    void fetchTenantModules({ apiBaseUrl, bearerToken }, tenantId).then((r) => {
      if (cancelled) return;
      if (r.kind === 'ok' && r.data.enabledModules.length > 0) {
        setState({
          enabledModules: r.data.enabledModules,
          hasSeats: r.data.enabledModules.includes('Seats'),
          loading: false,
        });
      } else {
        // Unknown / failure → stay Parking-only so no seat surfaces appear unexpectedly.
        setState({ enabledModules: ['Parking'], hasSeats: false, loading: false });
      }
    });
    return () => { cancelled = true; };
  }, [ready, apiBaseUrl, bearerToken, tenantId, isConfigured]);

  return state;
}
