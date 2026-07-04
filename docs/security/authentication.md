# Authentication

FairSpot uses OIDC/OAuth 2.0 with JWT bearer tokens. Web and mobile clients use Authorization Code + PKCE. Backend services validate JWT signature, issuer, audience, expiry, tenant claim, and role claims independently.

## Token Issuer

The token issuer is selected by deployment profile:

- local and current hosted evaluation profiles use Keycloak;
- client-owned production may use the client's approved OIDC provider, such as Microsoft Entra ID, Okta, Keycloak, or equivalent;
- FairSpot services must not depend on a cloud API gateway to mint application tokens.

## Required Claims

Services require:

- stable user subject / user ID;
- `tenant_id`;
- role claims mapped to FairSpot roles;
- issuer and audience matching the configured environment.

Tenant or user identity must never come from request bodies, query strings, or caller-supplied headers.

## Token Expiration

- Access tokens should be short-lived.
- Refresh behavior is owned by the web/mobile client and IdP configuration.
- Logout clears local client session state; server-side revocation depends on the selected IdP.
- Missing, expired, invalid, or claim-incomplete tokens return unauthorized responses.

## Secure Token Handling

- Do not log access tokens, refresh tokens, authorization codes, or client secrets.
- Do not place tokens in URLs.
- Use HTTPS for all hosted public endpoints.
- Store browser/mobile session material only in platform-appropriate secure storage.
- Do not store company passwords in FairSpot. Company credentials remain with the client's IdP.
