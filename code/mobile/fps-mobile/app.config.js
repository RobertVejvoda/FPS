const appJson = require('./app.json');

function env(name, fallback) {
  const value = process.env[name];
  return value && value.trim().length > 0 ? value.trim() : fallback;
}

module.exports = () => ({
  ...appJson.expo,
  extra: {
    ...appJson.expo.extra,
    authIssuerUrl: env(
      'FPS_MOBILE_AUTH_ISSUER_URL',
      env('EXPO_PUBLIC_AUTH_ISSUER_URL', appJson.expo.extra.authIssuerUrl),
    ),
    authClientId: env(
      'FPS_MOBILE_AUTH_CLIENT_ID',
      env('EXPO_PUBLIC_AUTH_CLIENT_ID', appJson.expo.extra.authClientId),
    ),
    authScopes: env(
      'FPS_MOBILE_AUTH_SCOPES',
      env('EXPO_PUBLIC_AUTH_SCOPES', appJson.expo.extra.authScopes),
    ),
    apiBaseUrl: env(
      'FPS_MOBILE_API_BASE_URL',
      env('EXPO_PUBLIC_API_BASE_URL', appJson.expo.extra.apiBaseUrl),
    ),
  },
});
