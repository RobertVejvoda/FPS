import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  fetchPlatformTenants,
  fetchPlatformTenantDetail,
  fetchTenantRequests,
  decideTenantRequest,
  normalizeRequestStatus,
  isDecidable,
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
