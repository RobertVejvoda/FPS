import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  fetchPlatformTenants,
  fetchPlatformTenantDetail,
  fetchTenantRequests,
  decideTenantRequest,
  normalizeRequestStatus,
  isDecidable,
  fetchPlatformUsageStats,
  fetchSandboxResetEvidence,
  currentMonthKey,
  summarizeTenantReadiness,
  tenantReadinessStatus,
  summarizeUsage,
  findSandboxTenant,
  sandboxFreshnessStatus,
  formatRelativeAge,
  drawHealthStatus,
  fetchPlatformDrawHealth,
  type PlatformTenantRow,
  type PlatformUsageRow,
  type SandboxResetEvidence,
  type PlatformDrawHealth,
} from './platform';

// PLAT008B — the platform directory/detail API maps HTTP outcomes to FetchResult kinds the
// pages render as loading/ok/empty/error/unauthenticated states, and sends the platform bearer.

function mockFetch(status: number, body: unknown) {
  return vi.fn().mockResolvedValue({
    status,
    ok: status >= 200 && status < 300,
    json: async () => body,
  } as Response);
}

const cfg = { apiBaseUrl: 'http://api', bearerToken: 'plat-token' };

afterEach(() => { vi.restoreAllMocks(); });

describe('fetchPlatformTenants', () => {
  it('maps 200 to ok with the tenant rows and sends the bearer token', async () => {
    const rows = [{ tenantId: 'globex', slug: 'globex', displayName: 'Globex', region: 'eu', timeZone: 'Europe/Prague', kind: 'Production', lifecycleState: 'Ready', createdAt: '', updatedAt: '' }];
    const fetchMock = mockFetch(200, rows);
    vi.stubGlobal('fetch', fetchMock);
    const result = await fetchPlatformTenants(cfg);
    expect(result).toEqual({ kind: 'ok', data: rows });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api/platform/tenants');
    expect(init.headers.Authorization).toBe('Bearer plat-token');
  });

  it('maps 200 with empty array to ok (empty directory)', async () => {
    vi.stubGlobal('fetch', mockFetch(200, []));
    expect(await fetchPlatformTenants(cfg)).toEqual({ kind: 'ok', data: [] });
  });

  it('maps 403 to unauthenticated', async () => {
    vi.stubGlobal('fetch', mockFetch(403, {}));
    expect(await fetchPlatformTenants(cfg)).toEqual({ kind: 'unauthenticated' });
  });

  it('returns unauthenticated without a token (no request made)', async () => {
    const fetchMock = mockFetch(200, []);
    vi.stubGlobal('fetch', fetchMock);
    expect(await fetchPlatformTenants({ apiBaseUrl: 'http://api', bearerToken: '' })).toEqual({ kind: 'unauthenticated' });
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe('fetchPlatformTenantDetail', () => {
  it('maps 404 to a not-found error', async () => {
    vi.stubGlobal('fetch', mockFetch(404, {}));
    const result = await fetchPlatformTenantDetail(cfg, 'missing');
    expect(result).toEqual({ kind: 'error', status: 404, message: 'Not found.' });
  });

  it('encodes the tenant id in the path', async () => {
    const fetchMock = mockFetch(200, {});
    vi.stubGlobal('fetch', fetchMock);
    await fetchPlatformTenantDetail(cfg, 'a/b');
    expect(fetchMock.mock.calls[0][0]).toBe('http://api/platform/tenants/a%2Fb');
  });
});

// PLAT008C onboarding queue

describe('normalizeRequestStatus / isDecidable', () => {
  it('maps the numeric enum (and string fallback) to a status label', () => {
    expect(normalizeRequestStatus(0)).toBe('Requested');
    expect(normalizeRequestStatus(1)).toBe('Approved');
    expect(normalizeRequestStatus(2)).toBe('Rejected');
    expect(normalizeRequestStatus('Approved')).toBe('Approved');
  });
  it('only Requested items are decidable', () => {
    expect(isDecidable('Requested')).toBe(true);
    expect(isDecidable('Approved')).toBe(false);
    expect(isDecidable('Rejected')).toBe(false);
  });
});

describe('fetchTenantRequests', () => {
  it('normalizes status and drops the raw actor hash from the typed result', async () => {
    const wire = [{
      requestId: 'r1', company: 'Globex', primaryDomain: 'globex.com', contactEmail: 'a@globex.com',
      message: 'hi', status: 0, createdAt: '2026-06-30T00:00:00Z', decidedAt: null, decisionReason: null,
      decidedByHash: 'SHOULD-NOT-SURFACE',
    }];
    vi.stubGlobal('fetch', mockFetch(200, wire));
    const r = await fetchTenantRequests(cfg);
    expect(r.kind).toBe('ok');
    if (r.kind === 'ok') {
      expect(r.data[0].status).toBe('Requested');
      expect(JSON.stringify(r.data[0])).not.toContain('decidedByHash');
      expect(JSON.stringify(r.data[0])).not.toContain('SHOULD-NOT-SURFACE');
    }
  });

  it('maps 403 (auditor / unauthorized) to unauthenticated', async () => {
    vi.stubGlobal('fetch', mockFetch(403, {}));
    expect((await fetchTenantRequests(cfg)).kind).toBe('unauthenticated');
  });
});

describe('decideTenantRequest', () => {
  it('POSTs the reason to the approve/reject path with the bearer token', async () => {
    const fetchMock = mockFetch(200, {});
    vi.stubGlobal('fetch', fetchMock);
    await decideTenantRequest(cfg, 'r1', 'approve', 'looks good');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api/tenant-requests/r1/approve');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({ reason: 'looks good' });
    expect(init.headers.Authorization).toBe('Bearer plat-token');
  });

  it('surfaces the backend error detail on a 400', async () => {
    vi.stubGlobal('fetch', mockFetch(400, { error: 'Request already decided.' }));
    const r = await decideTenantRequest(cfg, 'r1', 'reject', '');
    expect(r).toEqual({ kind: 'error', status: 400, message: 'Request already decided.' });
  });
});

// PLAT008D — platform health strip sources + status normalization.

function tenant(overrides: Partial<PlatformTenantRow>): PlatformTenantRow {
  return {
    tenantId: 't', slug: 't', displayName: 'T', region: 'eu', timeZone: 'Europe/Prague',
    kind: 'Production', lifecycleState: 'Ready',
    primaryModule: 'Parking', enabledModules: ['Parking'],
    createdAt: '', updatedAt: '', ...overrides,
  };
}

describe('currentMonthKey', () => {
  it('formats the UTC year-month as YYYY-MM', () => {
    expect(currentMonthKey(new Date('2026-07-02T12:00:00Z'))).toBe('2026-07');
    expect(currentMonthKey(new Date('2026-01-31T23:59:59Z'))).toBe('2026-01');
  });
});

describe('summarizeTenantReadiness / tenantReadinessStatus', () => {
  it('counts states and only flags Suspended tenants as a warning', () => {
    const rows = [tenant({ lifecycleState: 'Ready' }), tenant({ lifecycleState: 'Ready' }), tenant({ lifecycleState: 'Draft' })];
    const s = summarizeTenantReadiness(rows);
    expect(s).toMatchObject({ total: 3, ready: 2, suspended: 0 });
    expect(s.byState).toEqual({ Ready: 2, Draft: 1 });
    expect(tenantReadinessStatus(s)).toBe('ok');
  });

  it('warns when any tenant is Suspended', () => {
    const s = summarizeTenantReadiness([tenant({ lifecycleState: 'Ready' }), tenant({ lifecycleState: 'Suspended' })]);
    expect(s.suspended).toBe(1);
    expect(tenantReadinessStatus(s)).toBe('warning');
  });

  it('an empty directory is OK, not a warning', () => {
    expect(tenantReadinessStatus(summarizeTenantReadiness([]))).toBe('ok');
  });
});

describe('summarizeUsage', () => {
  it('sums activity across tenants and counts tenants with any activity', () => {
    const rows: PlatformUsageRow[] = [
      { tenantId: 'a', period: '2026-07', activeRequestorCount: 3, bookingRequestCount: 5, drawRunCount: 1, allocatedCount: 0, rejectedCount: 0, cancelledCount: 0, expiredCount: 0, noShowCount: 0, usedCount: 0, lastUpdatedAt: '' },
      { tenantId: 'b', period: '2026-07', activeRequestorCount: 0, bookingRequestCount: 0, drawRunCount: 0, allocatedCount: 0, rejectedCount: 0, cancelledCount: 0, expiredCount: 0, noShowCount: 0, usedCount: 0, lastUpdatedAt: '' },
    ];
    expect(summarizeUsage(rows)).toEqual({ activeRequestors: 3, bookingRequests: 5, drawRuns: 1, tenantsWithActivity: 1 });
  });
});

describe('findSandboxTenant', () => {
  it('finds the tenant whose kind is Sandbox (no hard-coded slug)', () => {
    const rows = [tenant({ tenantId: 'prod', kind: 'Production' }), tenant({ tenantId: 'gl', kind: 'Sandbox' })];
    expect(findSandboxTenant(rows)?.tenantId).toBe('gl');
  });
  it('returns null when no sandbox tenant exists', () => {
    expect(findSandboxTenant([tenant({ kind: 'Production' })])).toBeNull();
  });
});

describe('sandboxFreshnessStatus', () => {
  const base: SandboxResetEvidence = { tenantId: 'gl', status: 'Succeeded', source: 'manual', startedAt: '', completedAt: '', snapshotVersion: 'gl-v1', failureReason: null };
  it('OK only for a Succeeded reset', () => {
    expect(sandboxFreshnessStatus(base)).toBe('ok');
    expect(sandboxFreshnessStatus({ ...base, status: 'Failed' })).toBe('warning');
    expect(sandboxFreshnessStatus({ ...base, status: 'Unavailable' })).toBe('warning');
  });
});

// PLAT008E — draw health status derivation + fetcher path.
describe('drawHealthStatus', () => {
  const base: PlatformDrawHealth = { windowDays: 7, completedCount: 5, failedCount: 0, runningCount: 0, stuckCount: 0, lastFailureAt: null, lastActivityAt: null };
  it('OK when there are no failed or stuck draws', () => {
    expect(drawHealthStatus(base)).toBe('ok');
    expect(drawHealthStatus({ ...base, completedCount: 0 })).toBe('ok');
    expect(drawHealthStatus({ ...base, runningCount: 2 })).toBe('ok'); // running (not yet stuck) is fine
  });
  it('warns on any failed or stuck draw', () => {
    expect(drawHealthStatus({ ...base, failedCount: 1 })).toBe('warning');
    expect(drawHealthStatus({ ...base, stuckCount: 1 })).toBe('warning');
  });
});

describe('fetchPlatformDrawHealth', () => {
  it('requests the platform draw-health endpoint with the window and bearer token', async () => {
    const fetchMock = mockFetch(200, { windowDays: 7, completedCount: 3, failedCount: 0, runningCount: 0, stuckCount: 0, lastFailureAt: null, lastActivityAt: null });
    vi.stubGlobal('fetch', fetchMock);
    await fetchPlatformDrawHealth(cfg, 14);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api/datahub/platform/draw-health?windowDays=14');
    expect(init.headers.Authorization).toBe('Bearer plat-token');
  });
});

describe('formatRelativeAge', () => {
  const now = new Date('2026-07-10T00:00:00Z');
  it('renders coarse day-level ages', () => {
    expect(formatRelativeAge('2026-07-10T00:00:00Z', now)).toBe('today');
    expect(formatRelativeAge('2026-07-09T00:00:00Z', now)).toBe('yesterday');
    expect(formatRelativeAge('2026-07-07T00:00:00Z', now)).toBe('3 days ago');
    expect(formatRelativeAge('not-a-date', now)).toBe('unknown');
  });
});

describe('fetchPlatformUsageStats', () => {
  it('requests the month-scoped platform usage endpoint with the bearer token', async () => {
    const fetchMock = mockFetch(200, []);
    vi.stubGlobal('fetch', fetchMock);
    await fetchPlatformUsageStats(cfg, '2026-07');
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api/datahub/platform/usage-stats?month=2026-07');
    expect(init.headers.Authorization).toBe('Bearer plat-token');
  });
});

describe('fetchSandboxResetEvidence', () => {
  it('maps 404 (no reset recorded) to a not-found error the card treats as no-evidence', async () => {
    vi.stubGlobal('fetch', mockFetch(404, {}));
    const r = await fetchSandboxResetEvidence(cfg, 'gl');
    expect(r).toEqual({ kind: 'error', status: 404, message: 'Not found.' });
  });
});
