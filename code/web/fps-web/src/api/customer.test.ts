import { afterEach, describe, expect, it, vi } from 'vitest';
import { submitPilotRequest } from './customer';

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
