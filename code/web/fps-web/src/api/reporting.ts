import type { ApiClientConfig, FetchResult } from './client';

export interface DashboardResponse {
  totalDemand: number;
  totalAllocations: number;
  totalRejections: number;
  totalCancellations: number;
  totalNoShows: number;
  totalPenalties: number;
  overallAllocationRate: number;
  rejectionsByReason: Record<string, number>;
  dailyTrend: { date: string; demand: number; allocations: number; allocationRate: number }[];
}

export interface SummaryItem {
  date: string;
  locationId: string;
  timeSlot: string;
  demandCount: number;
  allocationCount: number;
  allocationRate: number;
  rejectionCount: number;
  cancellationCount: number;
  noShowCount: number;
  penaltyCount: number;
}

export async function fetchReportingDashboard(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<DashboardResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/reports/parking/dashboard`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /reports/parking/dashboard returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as DashboardResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function downloadCsvReport(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<{ kind: 'ok'; blob: Blob } | { kind: 'unauthenticated' } | { kind: 'error'; message: string } | { kind: 'unreachable'; message: string }> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/reports/parking/summary.csv`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'text/csv' },
    });
    if (res.status === 401 || res.status === 403) return { kind: 'unauthenticated' };
    if (!res.ok) return { kind: 'error', message: `GET /reports/parking/summary.csv returned ${res.status}` };
    return { kind: 'ok', blob: await res.blob() };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}
