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

export interface SummaryResponse {
  items: SummaryItem[];
}

export interface FairnessEntry {
  requestorHash: string;
  requestCount: number;
  allocationCount: number;
  allocationRate: number;
}

export interface FairnessResponse {
  items: FairnessEntry[];
}

export interface UtilizationEntry {
  locationId: string;
  totalDemand: number;
  totalAllocations: number;
  totalRejections: number;
  totalCancellations: number;
  totalNoShows: number;
  allocationRate: number;
}

export interface UtilizationResponse {
  items: UtilizationEntry[];
}

export interface ReasonCodeEntry {
  reasonCode: string;
  count: number;
  rateOfDemand: number;
}

export interface ReasonCodeResponse {
  items: ReasonCodeEntry[];
  totalDemand: number;
}

type CsvResult = { kind: 'ok'; blob: Blob } | { kind: 'unauthenticated' } | { kind: 'error'; message: string; status?: number } | { kind: 'unreachable'; message: string };

function authHeaders(bearerToken: string) {
  return { Authorization: `Bearer ${bearerToken}`, Accept: 'application/json' };
}

async function fetchCsv(apiBaseUrl: string, bearerToken: string, path: string): Promise<CsvResult> {
  try {
    const res = await fetch(`${apiBaseUrl}${path}`, {
      headers: { Authorization: `Bearer ${bearerToken}`, Accept: 'text/csv' },
    });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', message: `GET ${path} returned ${res.status}` };
    return { kind: 'ok', blob: await res.blob() };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchReportingDashboard(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<DashboardResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/reports/parking/dashboard`, { headers: authHeaders(bearerToken) });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /reports/parking/dashboard returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as DashboardResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchReportingSummary(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<SummaryResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/reports/parking/summary`, { headers: authHeaders(bearerToken) });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /reports/parking/summary returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as SummaryResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchReportingFairness(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<FairnessResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/reports/parking/fairness`, { headers: authHeaders(bearerToken) });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /reports/parking/fairness returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as FairnessResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchUtilizationReport(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<UtilizationResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/reports/parking/utilization`, { headers: authHeaders(bearerToken) });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /reports/parking/utilization returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as UtilizationResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function fetchReasonCodeReport(
  { apiBaseUrl, bearerToken }: ApiClientConfig,
): Promise<FetchResult<ReasonCodeResponse>> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  try {
    const res = await fetch(`${apiBaseUrl}/reports/parking/reason-codes`, { headers: authHeaders(bearerToken) });
    if (res.status === 401) return { kind: 'unauthenticated' };
    if (res.status === 403) return { kind: 'error', status: 403, message: 'Insufficient permissions.' };
    if (!res.ok) return { kind: 'error', status: res.status, message: `GET /reports/parking/reason-codes returned ${res.status}` };
    return { kind: 'ok', data: (await res.json()) as ReasonCodeResponse };
  } catch (e) {
    return { kind: 'unreachable', message: e instanceof Error ? e.message : 'network error' };
  }
}

export async function downloadCsvReport({ apiBaseUrl, bearerToken }: ApiClientConfig): Promise<CsvResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  return fetchCsv(apiBaseUrl, bearerToken, '/reports/parking/summary.csv');
}

export async function downloadAllocationOutcomesCsv({ apiBaseUrl, bearerToken }: ApiClientConfig): Promise<CsvResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  return fetchCsv(apiBaseUrl, bearerToken, '/reports/parking/allocation-outcomes.csv');
}
