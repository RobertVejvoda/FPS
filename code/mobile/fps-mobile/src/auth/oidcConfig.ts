import Constants from 'expo-constants';

export type OidcConfig = {
  issuerUrl: string;
  clientId: string;
  scopes: string[];
  apiBaseUrl: string;
};

function extra(key: string): string {
  return (Constants.expoConfig?.extra?.[key] as string | undefined) ?? '';
}

export function getOidcConfig(): OidcConfig {
  return {
    issuerUrl: extra('authIssuerUrl'),
    clientId: extra('authClientId'),
    scopes: extra('authScopes').split(' ').filter(Boolean),
    apiBaseUrl: extra('apiBaseUrl'),
  };
}

export function isOidcConfigured(config: OidcConfig): boolean {
  return config.issuerUrl.length > 0 && config.clientId.length > 0 && config.apiBaseUrl.length > 0;
}
