#!/bin/sh
# dev-env.sh — Export local development environment variables for FPS backend services.
# Source this before running services so they validate tokens from the local Keycloak.
#
# Usage:
#   source ./tools/dev-env.sh
#   dotnet run --project code/server/Identity/FPS.Identity/FPS.Identity.csproj
#
# ASP.NET Core reads Auth__Authority and Auth__Audience from environment,
# overriding any appsettings values. Double-underscore maps to nested keys.

KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8180}"
REALM="${FPS_LOCAL_REALM:-fps-local}"
CLIENT_ID="${FPS_LOCAL_CLIENT:-fps-mobile-dev}"
ADDITIONAL_CLIENTS="${FPS_LOCAL_ADDITIONAL_CLIENTS:-fps-web-dev}"

export Auth__Authority="$KEYCLOAK_URL/realms/$REALM"
export Auth__Audience="$CLIENT_ID"
export Auth__AdditionalAudiences="$ADDITIONAL_CLIENTS"
export Auth__AllowLocalIssuerHostOverride="${FPS_LOCAL_ALLOW_ISSUER_HOST_OVERRIDE:-true}"
# PLAT001: the FairSpot-controlled dev realm's privileged roles may pass through for tenants
# without an explicit mapping (the shared ConfiguredTenantRoleMapper strips privileged roles
# otherwise). Without this, HR/admin/auditor flows 403 in non-Customer services.
export Auth__TrustedRealmRoles="${FPS_AUTH_TRUSTED_REALM_ROLES:-admin,hr_manager,auditor,report_viewer}"
export ASPNETCORE_ENVIRONMENT="Development"

echo "FPS local environment set:"
echo "  Auth__Authority=$Auth__Authority"
echo "  Auth__Audience=$Auth__Audience"
echo "  Auth__AdditionalAudiences=$Auth__AdditionalAudiences"
echo "  Auth__AllowLocalIssuerHostOverride=$Auth__AllowLocalIssuerHostOverride"
echo "  Auth__TrustedRealmRoles=$Auth__TrustedRealmRoles"
