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
  environment?: string;
  simulationEnabled?: boolean;
  appVersion?: string;
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
  const environment = typeof r['environment'] === 'string' ? r['environment'] : undefined;
  const simulationEnabled = typeof r['simulationEnabled'] === 'boolean' ? r['simulationEnabled'] : false;
  const appVersion = typeof r['appVersion'] === 'string' ? r['appVersion'] : undefined;
  const branding = validateBranding(r['branding']);
  return { apiBaseUrl, oidc, branding, devTokenFallbackEnabled, environment, simulationEnabled, appVersion };
}

function optionalString(obj: Record<string, unknown>, key: string, fallback: string): string {
  const val = obj[key];
  return typeof val === 'string' ? val.trim() : fallback;
}

function validateBranding(raw: unknown): BrandingConfig {
  const defaults: BrandingConfig = {
    productName: 'FairSpot',
    tenantName: '',
    logoUrl: '/brand/fairspot-app-icon.svg',
    primaryColor: '#2f7d3f',
    accentColor: '#43b75a',
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
