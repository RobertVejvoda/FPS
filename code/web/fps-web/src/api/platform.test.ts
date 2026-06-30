import { afterEach, describe, expect, it, vi } from 'vitest';
import { fetchPlatformTenants, fetchPlatformTenantDetail } from './platform';

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
