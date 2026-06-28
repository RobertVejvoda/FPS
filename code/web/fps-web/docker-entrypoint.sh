#!/bin/sh
# Generate /config.json from FPS_WEB_* environment variables at container start.
#
# If FPS_WEB_API_BASE_URL is unset, the default config.json baked from
# public/config.json is left in place (useful for quick local container runs).
#
# Runs from nginx:alpine's /docker-entrypoint.d/ before nginx starts.
set -eu

CONFIG_PATH="/usr/share/nginx/html/config.json"

if [ -z "${FPS_WEB_API_BASE_URL:-}" ]; then
  echo "[fps-config] FPS_WEB_API_BASE_URL not set — serving baked default config.json"
  exit 0
fi

: "${FPS_WEB_OIDC_AUTHORITY:?FPS_WEB_OIDC_AUTHORITY is required when FPS_WEB_API_BASE_URL is set}"
: "${FPS_WEB_OIDC_CLIENT_ID:?FPS_WEB_OIDC_CLIENT_ID is required when FPS_WEB_API_BASE_URL is set}"
: "${FPS_WEB_OIDC_REDIRECT_URI:?FPS_WEB_OIDC_REDIRECT_URI is required when FPS_WEB_API_BASE_URL is set}"
: "${FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI:?FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI is required when FPS_WEB_API_BASE_URL is set}"

cat > "$CONFIG_PATH" <<EOF
{
  "apiBaseUrl": "${FPS_WEB_API_BASE_URL}",
  "oidc": {
    "authority": "${FPS_WEB_OIDC_AUTHORITY}",
    "clientId": "${FPS_WEB_OIDC_CLIENT_ID}",
    "scopes": "${FPS_WEB_OIDC_SCOPES:-openid profile email}",
    "redirectUri": "${FPS_WEB_OIDC_REDIRECT_URI}",
    "postLogoutRedirectUri": "${FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI}"
  },
  "branding": {
    "productName": "${FPS_WEB_PRODUCT_NAME:-FairSpot}",
    "tenantName": "${FPS_WEB_TENANT_NAME:-}",
    "logoUrl": "${FPS_WEB_LOGO_URL:-/brand/fairspot-app-icon.svg}",
    "primaryColor": "${FPS_WEB_PRIMARY_COLOR:-#2f7d3f}",
    "accentColor": "${FPS_WEB_ACCENT_COLOR:-#43b75a}"
  },
  "devTokenFallbackEnabled": ${FPS_WEB_DEV_TOKEN_FALLBACK:-false},
  "environment": "${FPS_WEB_ENVIRONMENT:-Production}"
}
EOF

echo "[fps-config] wrote $CONFIG_PATH for apiBaseUrl=${FPS_WEB_API_BASE_URL}"
