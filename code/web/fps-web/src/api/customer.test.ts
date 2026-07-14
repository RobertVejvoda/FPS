import { afterEach, describe, expect, it, vi } from 'vitest';
import { fetchTenantIdentityConfig, saveTenantIdentityConfig, submitPilotRequest } from './customer';
import type { TenantIdentityConfigInput } from './customer';

// PLAT004c — the public pilot request maps HTTP outcomes to business-readable result kinds.
// These pin the status→kind contract the page relies on (202 ack, 400 detail, 429 rate-limit).

function mockFetch(status: number, body: unknown) {
  return vi.fn().mockResolvedValue({
    status,
    ok: status >= 200 && status < 300,
    json: async () => body,
  } as Response);
}

afterEach(() => { vi.restoreAllMocks(); });

const input = {
  companyName: 'Green Logistics',
  companyDomain: 'greenlogistics.com',
  workEmail: 'jo@greenlogistics.com',
  message: '30 sites, fair allocation',
  verificationToken: 'tok',
};

describe('submitPilotRequest', () => {
  it('maps 202 to ok with the acknowledgement reference', async () => {
    vi.stubGlobal('fetch', mockFetch(202, { requestId: 'abc123', status: 'Requested' }));
    const result = await submitPilotRequest('http://api', input);
    expect(result).toEqual({ kind: 'ok', reference: 'abc123' });
  });

  it('sends the business fields under the API wire names with no auth header', async () => {
    const fetchMock = mockFetch(202, { requestId: 'r', status: 'Requested' });
    vi.stubGlobal('fetch', fetchMock);
    await submitPilotRequest('http://api', input);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api/tenant-requests');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({
      company: 'Green Logistics',
      primaryDomain: 'greenlogistics.com',
      contactEmail: 'jo@greenlogistics.com',
      message: '30 sites, fair allocation',
      turnstileToken: 'tok',
    });
    expect(init.headers).not.toHaveProperty('Authorization');
  });

  it('surfaces the human detail from a 400 problem response', async () => {
    vi.stubGlobal('fetch', mockFetch(400, { detail: 'A valid contact email is required.' }));
    const result = await submitPilotRequest('http://api', input);
    expect(result).toEqual({ kind: 'invalid', message: 'A valid contact email is required.' });
  });

  it('maps 429 to rate-limited', async () => {
    vi.stubGlobal('fetch', mockFetch(429, null));
    const result = await submitPilotRequest('http://api', input);
    expect(result).toEqual({ kind: 'rate-limited' });
  });

  it('maps a network failure to error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')));
    const result = await submitPilotRequest('http://api', input);
    expect(result).toEqual({ kind: 'error' });
  });

  it('sends a null token when none is provided (dev / no site key)', async () => {
    const fetchMock = mockFetch(202, { requestId: 'r', status: 'Requested' });
    vi.stubGlobal('fetch', fetchMock);
    await submitPilotRequest('http://api', { ...input, verificationToken: '' });
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).turnstileToken).toBeNull();
  });
});

// AUTH012 (#795) — tenant-admin identity settings API helpers. These pin the
// status→kind contract the settings form relies on (200 config, 404 empty form,
// 204 saved, 400 business-readable validation message).

const cfg = { apiBaseUrl: 'http://api', bearerToken: 'tok' };

const identityInput: TenantIdentityConfigInput = {
  trustedIssuer: 'https://auth.example.com/realms/fairspot',
  audience: 'fairspot-api',
  tenantClaimName: 'tenant_id',
  subjectClaimName: 'sub',
  roleClaimNames: ['groups'],
  roleMapping: { 'fairspot-admins': 'admin' },
  localAccountPolicyEnabled: true,
  idpBrokerAlias: 'acme-entra',
};

describe('fetchTenantIdentityConfig', () => {
  it('maps 200 to ok with the config payload', async () => {
    const payload = { tenantId: 't1', trustedIssuer: 'iss', audience: 'aud', tenantClaimName: 'tenant_id', subjectClaimName: 'sub', roleClaimNames: [], roleMapping: {}, localAccountPolicyEnabled: false, idpBrokerAlias: 'acme-entra', configuredByHash: 'h', configuredAt: 'now', updatedAt: null };
    vi.stubGlobal('fetch', mockFetch(200, payload));
    const result = await fetchTenantIdentityConfig(cfg, 't1');
    expect(result).toEqual({ kind: 'ok', data: payload });
  });

  it('maps 404 to notconfigured — an empty setup form, not an error', async () => {
    vi.stubGlobal('fetch', mockFetch(404, null));
    const result = await fetchTenantIdentityConfig(cfg, 't1');
    expect(result).toEqual({ kind: 'notconfigured' });
  });

  it('maps 401 to unauthenticated', async () => {
    vi.stubGlobal('fetch', mockFetch(401, null));
    const result = await fetchTenantIdentityConfig(cfg, 't1');
    expect(result).toEqual({ kind: 'unauthenticated' });
  });
});

describe('saveTenantIdentityConfig', () => {
  it('maps 204 to ok and sends the wire body with idpBrokerAlias', async () => {
    const fetchMock = mockFetch(204, null);
    vi.stubGlobal('fetch', fetchMock);
    const result = await saveTenantIdentityConfig(cfg, 't1', identityInput);
    expect(result).toEqual({ kind: 'ok' });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api/tenants/t1/identity-config');
    expect(init.method).toBe('PUT');
    expect(JSON.parse(init.body)).toEqual({
      trustedIssuer: identityInput.trustedIssuer,
      audience: identityInput.audience,
      tenantClaimName: 'tenant_id',
      subjectClaimName: 'sub',
      roleClaimNames: ['groups'],
      roleMapping: { 'fairspot-admins': 'admin' },
      idpBrokerAlias: 'acme-entra',
      localAccountPolicyEnabled: true,
    });
  });

  it('sends null for an unconfigured broker alias', async () => {
    const fetchMock = mockFetch(204, null);
    vi.stubGlobal('fetch', fetchMock);
    await saveTenantIdentityConfig(cfg, 't1', { ...identityInput, idpBrokerAlias: null });
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).idpBrokerAlias).toBeNull();
  });

  it('surfaces the server validation message from a 400 response', async () => {
    vi.stubGlobal('fetch', mockFetch(400, { error: 'IdpBrokerAlias must be 1-64 characters: alphanumerics, dot, underscore, or hyphen, starting alphanumeric.' }));
    const result = await saveTenantIdentityConfig(cfg, 't1', identityInput);
    expect(result.kind).toBe('invalid');
    if (result.kind === 'invalid') expect(result.message).toContain('IdpBrokerAlias');
  });

  it('maps 401 to unauthenticated', async () => {
    vi.stubGlobal('fetch', mockFetch(401, null));
    const result = await saveTenantIdentityConfig(cfg, 't1', identityInput);
    expect(result).toEqual({ kind: 'unauthenticated' });
  });
});
