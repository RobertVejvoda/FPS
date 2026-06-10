import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { getSimulationStatus, type SimulationStatus } from '../api/simulation';

export type TenantDateContext = {
  dateBase: Date;
  simulationActive: boolean;
};

export function useTenantDateContext(): TenantDateContext {
  const { apiBaseUrl, bearerToken, simulationEnabled } = useAuth();
  const [simulationStatus, setSimulationStatus] = useState<SimulationStatus | null>(null);

  useEffect(() => {
    if (!simulationEnabled || !bearerToken) {
      setSimulationStatus(null);
      return;
    }

    let cancelled = false;
    const refreshSimulationStatus = () => getSimulationStatus({ apiBaseUrl, bearerToken }).then(result => {
      if (!cancelled) {
        setSimulationStatus(result.kind === 'ok' ? result.data : null);
      }
    });

    void refreshSimulationStatus();
    const interval = window.setInterval(() => { void refreshSimulationStatus(); }, 10_000);
    window.addEventListener('focus', refreshSimulationStatus);

    return () => {
      cancelled = true;
      window.clearInterval(interval);
      window.removeEventListener('focus', refreshSimulationStatus);
    };
  }, [apiBaseUrl, bearerToken, simulationEnabled]);

  return useMemo(() => {
    const source = simulationStatus?.simulationActive && simulationStatus.virtualNow
      ? simulationStatus.virtualNow
      : undefined;
    return {
      dateBase: source ? new Date(source) : new Date(),
      simulationActive: simulationStatus?.simulationActive ?? false,
    };
  }, [simulationStatus]);
}

export function useTenantDateBase(): Date {
  return useTenantDateContext().dateBase;
}
