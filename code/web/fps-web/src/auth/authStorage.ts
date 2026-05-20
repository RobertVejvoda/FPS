const TOKEN_KEY = 'fps.bearerToken';
const BASE_URL_KEY = 'fps.apiBaseUrl';

export function loadToken(): string {
  return localStorage.getItem(TOKEN_KEY) ?? '';
}

export function loadBaseUrl(): string {
  return localStorage.getItem(BASE_URL_KEY) ?? '';
}

export function saveCredentials(apiBaseUrl: string, bearerToken: string): void {
  localStorage.setItem(BASE_URL_KEY, apiBaseUrl.trim().replace(/\/+$/, ''));
  localStorage.setItem(TOKEN_KEY, bearerToken.trim());
}

export function clearCredentials(): void {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(BASE_URL_KEY);
}
