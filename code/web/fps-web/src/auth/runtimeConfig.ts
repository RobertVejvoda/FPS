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
  branding: BrandingConfig;
  devTokenFallbackEnabled: boolean;
};

export type BrandingConfig = {
  productName: string;
  tenantName: string;
  logoUrl: string;
  primaryColor: string;
  accentColor: string;
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
  const branding = validateBranding(r['branding']);
  return { apiBaseUrl, oidc, branding, devTokenFallbackEnabled };
}

function optionalString(obj: Record<string, unknown>, key: string, fallback: string): string {
  const val = obj[key];
  return typeof val === 'string' ? val.trim() : fallback;
}

function validateBranding(raw: unknown): BrandingConfig {
  const defaults: BrandingConfig = {
    productName: 'FairSpot',
    tenantName: '',
    logoUrl: '',
    primaryColor: '#2563eb',
    accentColor: '#16a34a',
  };
  if (typeof raw !== 'object' || raw === null) return defaults;
  const b = raw as Record<string, unknown>;
  return {
    productName: optionalString(b, 'productName', defaults.productName) || defaults.productName,
    tenantName: optionalString(b, 'tenantName', defaults.tenantName),
    logoUrl: optionalString(b, 'logoUrl', defaults.logoUrl),
    primaryColor: optionalString(b, 'primaryColor', defaults.primaryColor) || defaults.primaryColor,
    accentColor: optionalString(b, 'accentColor', defaults.accentColor) || defaults.accentColor,
  };
}
