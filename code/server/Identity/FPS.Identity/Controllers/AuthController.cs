// AuthController removed.
//
// FPS uses an SSO-first model: employees authenticate through the customer IdP (Keycloak
// by default) using OIDC/OAuth 2.0. FPS services validate the resulting JWT bearer token;
// they do not issue tokens, store company passwords, or proxy credential operations.
//
// Local fallback accounts (demo, break-glass, small tenants without SSO) are managed as
// Keycloak local-realm users. Their credential verifiers stay within Keycloak's credential
// store; FPS Identity does not store or verify passwords.
//
// The authenticated user context after token validation is available via GET /me.
