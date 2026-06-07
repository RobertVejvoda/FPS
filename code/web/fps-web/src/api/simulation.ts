import type { ApiClientConfig } from './client';

export type SimulationStatus = {
  simulationActive: boolean;
  virtualNow: string | null;
  realNow: string;
};

type SimResult =
  | { kind: 'ok'; data: SimulationStatus }
  | { kind: 'not-available' }
  | { kind: 'error' };

async function callSimulation(cfg: ApiClientConfig, method: string, path: string, body?: object): Promise<SimResult> {
  try {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (cfg.bearerToken) headers['Authorization'] = `Bearer ${cfg.bearerToken}`;
    const res = await fetch(`${cfg.apiBaseUrl}/simulation/${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
    if (res.status === 404) return { kind: 'not-available' };
    if (!res.ok) return { kind: 'error' };
    const data = await res.json() as SimulationStatus;
    return { kind: 'ok', data };
  } catch {
    return { kind: 'error' };
  }
}

export function getSimulationStatus(cfg: ApiClientConfig): Promise<SimResult> {
  return callSimulation(cfg, 'GET', 'status');
}

export function advanceSimulation(cfg: ApiClientConfig, hours: number): Promise<SimResult> {
  return callSimulation(cfg, 'POST', 'advance', { hours });
}

export function resetSimulation(cfg: ApiClientConfig): Promise<SimResult> {
  return callSimulation(cfg, 'POST', 'reset');
}
