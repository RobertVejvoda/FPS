import type { ApiClientConfig, FetchResult } from './client';
import {
  fetchMetricsDashboard,
  fetchMetricsDaily,
  fetchMetricsUtilization,
  fetchMetricsReasonCodes,
  fetchMetricsEmployeeImpact,
  fetchMetricsOperationalExceptions,
} from './dataHub';

// ── Response interfaces ───────────────────────────────────────────────────────
// Shapes are preserved from the legacy Reporting API so ReportingPage.tsx
// requires no changes. The fetch functions below now delegate to DataHub
// metrics endpoints (DATAHUB006 / issue #334).

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
  // Raw requestor reference — the same id Profile uses as its user key.
  // Pass this to /profile/hr/display-names to resolve a display name; fall
  // back to displayRequestorRef(...) when no name is available (issue #474).
  requestorRef: string;
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

export interface EmployeeImpactEntry {
  requestorRef: string;
  totalRequests: number;
  totalRejections: number;
  totalAllocations: number;
}

export interface EmployeeImpactResponse {
  items: EmployeeImpactEntry[];
  minRejectionThreshold: number;
}

export interface OperationalExceptionEntry {
  date: string;
  locationId: string;
  exceptionType: string;
  description: string;
  totalDemand: number;
  totalAllocations: number;
  totalRejections: number;
}

export interface OperationalExceptionsResponse {
  items: OperationalExceptionEntry[];
}

type CsvResult = { kind: 'ok'; blob: Blob } | { kind: 'unauthenticated' } | { kind: 'error'; message: string; status?: number } | { kind: 'unreachable'; message: string };

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

// ── Fetch functions ───────────────────────────────────────────────────────────
// All JSON reads now go to DataHub metrics endpoints. CSV exports stay on the
// Reporting service (no DataHub export equivalent exists yet).

export async function fetchReportingDashboard(
  cfg: ApiClientConfig,
): Promise<FetchResult<DashboardResponse>> {
  const r = await fetchMetricsDashboard(cfg);
  if (r.kind !== 'ok') return r;
  const m = r.data;
  return {
    kind: 'ok',
    data: {
      totalDemand:          m.demand,
      totalAllocations:     m.allocated,
      totalRejections:      m.rejected,
      totalCancellations:   m.cancelled,
      totalNoShows:         m.noShow,
      totalPenalties:       0,
      overallAllocationRate: m.allocationRate / 100,
      // rejectionsByReason and dailyTrend are not in the dashboard aggregate;
      // the dedicated Reason Codes and Daily Summary sections in the page cover them.
      rejectionsByReason: {},
      dailyTrend:          [],
    },
  };
}

export async function fetchReportingSummary(
  cfg: ApiClientConfig,
): Promise<FetchResult<SummaryResponse>> {
  const r = await fetchMetricsDaily(cfg, { pageSize: 100 });
  if (r.kind !== 'ok') return r;
  return {
    kind: 'ok',
    data: {
      items: r.data.items.map(row => ({
        date:             row.date,
        locationId:       row.locationId,
        timeSlot:         row.timeSlot,
        demandCount:      row.demand,
        allocationCount:  row.allocated,
        allocationRate:   row.allocationRate / 100,
        rejectionCount:   row.rejected,
        cancellationCount: row.cancelled,
        noShowCount:      row.noShow,
        penaltyCount:     0,
      })),
    },
  };
}

export async function fetchReportingFairness(
  cfg: ApiClientConfig,
): Promise<FetchResult<FairnessResponse>> {
  const r = await fetchMetricsEmployeeImpact(cfg, { pageSize: 100 });
  if (r.kind !== 'ok') return r;
  return {
    kind: 'ok',
    data: {
      items: r.data.items.map(row => ({
        requestorRef:   row.requestorId,
        requestCount:   row.demand,
        allocationCount: row.allocated,
        allocationRate: row.allocationRate / 100,
      })),
    },
  };
}

export async function fetchUtilizationReport(
  cfg: ApiClientConfig,
): Promise<FetchResult<UtilizationResponse>> {
  const r = await fetchMetricsUtilization(cfg);
  if (r.kind !== 'ok') return r;
  return {
    kind: 'ok',
    data: {
      items: r.data.items.map(row => ({
        locationId:       row.locationId,
        totalDemand:      row.demand,
        totalAllocations: row.allocated,
        totalRejections:  row.rejected,
        totalCancellations: row.cancelled,
        totalNoShows:     0,
        allocationRate:   row.allocationRate / 100,
      })),
    },
  };
}

export async function fetchReasonCodeReport(
  cfg: ApiClientConfig,
): Promise<FetchResult<ReasonCodeResponse>> {
  const [rcResult, dashResult] = await Promise.all([
    fetchMetricsReasonCodes(cfg),
    fetchMetricsDashboard(cfg),
  ]);
  if (rcResult.kind !== 'ok') return rcResult;
  const totalDemand = dashResult.kind === 'ok' ? dashResult.data.demand : 0;
  const allEntries = [
    ...rcResult.data.rejections,
    ...rcResult.data.cancellations,
    ...rcResult.data.noShows,
  ];
  return {
    kind: 'ok',
    data: {
      items: allEntries.map(entry => ({
        reasonCode:   entry.reasonCode,
        count:        entry.count,
        rateOfDemand: totalDemand > 0 ? entry.count / totalDemand : 0,
      })),
      totalDemand,
    },
  };
}

export async function fetchEmployeeImpact(
  cfg: ApiClientConfig,
  minRejections = 2,
): Promise<FetchResult<EmployeeImpactResponse>> {
  const r = await fetchMetricsEmployeeImpact(cfg, { pageSize: 100 });
  if (r.kind !== 'ok') return r;
  const filtered = r.data.items.filter(row => row.rejected >= minRejections);
  return {
    kind: 'ok',
    data: {
      items: filtered.map(row => ({
        requestorRef:    row.requestorId,
        totalRequests:   row.demand,
        totalRejections: row.rejected,
        totalAllocations: row.allocated,
      })),
      minRejectionThreshold: minRejections,
    },
  };
}

export async function fetchOperationalExceptions(
  cfg: ApiClientConfig,
): Promise<FetchResult<OperationalExceptionsResponse>> {
  const r = await fetchMetricsOperationalExceptions(cfg);
  if (r.kind !== 'ok') return r;
  const items: OperationalExceptionEntry[] = [
    ...r.data.failedDraws.map(d => ({
      date:             d.date,
      locationId:       d.locationId,
      exceptionType:    'failed_draw',
      description:      d.safeFailureReason ?? 'Draw failed',
      totalDemand:      0,
      totalAllocations: 0,
      totalRejections:  0,
    })),
    ...r.data.zeroAllocationDraws.map(d => ({
      date:             d.date,
      locationId:       d.locationId,
      exceptionType:    'demand_no_allocations',
      description:      'Draw completed with no allocations',
      totalDemand:      d.rejectedCount + d.waitlistedCount,
      totalAllocations: 0,
      totalRejections:  d.rejectedCount,
    })),
  ];
  return { kind: 'ok', data: { items } };
}

// ── CSV exports (stay on Reporting service) ───────────────────────────────────

export async function downloadCsvReport({ apiBaseUrl, bearerToken }: ApiClientConfig): Promise<CsvResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  return fetchCsv(apiBaseUrl, bearerToken, '/reports/parking/summary.csv');
}

export async function downloadAllocationOutcomesCsv({ apiBaseUrl, bearerToken }: ApiClientConfig): Promise<CsvResult> {
  if (!apiBaseUrl || !bearerToken) return { kind: 'unauthenticated' };
  return fetchCsv(apiBaseUrl, bearerToken, '/reports/parking/allocation-outcomes.csv');
}
