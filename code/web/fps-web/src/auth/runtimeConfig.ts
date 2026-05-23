export type OidcConfig = {
  authority: string;
  clientId: string;
  scopes: string;
  redirectUri: string;
  postLogoutRedirectUri: string;
};

export type RuntimeConfig = {
  apiBaseUrl: string;
  oidc: OidcConfig;
  devTokenFallbackEnabled: boolean;
};

export async function loadRuntimeConfig(): Promise<RuntimeConfig> {
  const res = await fetch('/config.json');
  if (!res.ok) throw new Error(`/config.json returned ${res.status}`);
  const raw: unknown = await res.json();
  return validateConfig(raw);
}

function requireString(obj: Record<string, unknown>, key: string): string {
  const val = obj[key];
  if (typeof val !== 'string' || val.trim() === '') {
    throw new Error(`config.json: '${key}' is required and must be a non-empty string`);
  }
  return val;
}

function validateConfig(raw: unknown): RuntimeConfig {
  if (typeof raw !== 'object' || raw === null) {
    throw new Error('config.json must be an object');
  }
  const r = raw as Record<string, unknown>;
  const apiBaseUrl = requireString(r, 'apiBaseUrl');
  const oidcRaw = r['oidc'];
  if (typeof oidcRaw !== 'object' || oidcRaw === null) {
    throw new Error("config.json: 'oidc' must be an object");
  }
  const o = oidcRaw as Record<string, unknown>;
  const oidc: OidcConfig = {
    authority: requireString(o, 'authority'),
    clientId: requireString(o, 'clientId'),
    scopes: requireString(o, 'scopes'),
    redirectUri: requireString(o, 'redirectUri'),
    postLogoutRedirectUri: requireString(o, 'postLogoutRedirectUri'),
  };
  const devTokenFallbackEnabled =
    typeof r['devTokenFallbackEnabled'] === 'boolean' ? r['devTokenFallbackEnabled'] : false;
  return { apiBaseUrl, oidc, devTokenFallbackEnabled };
}
