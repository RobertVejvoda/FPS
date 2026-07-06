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
- Do not store customer or external IdP passwords in FairSpot. External credentials remain with the selected IdP.

## Multi-Factor Authentication and Passkeys

FairSpot relies on the OIDC provider for multi-factor authentication (MFA); it does **not** implement its own MFA, password, or passkey code. AUTH007 (#601) defines the policy — enforcement is configured in the identity provider.

Where MFA is enforced depends on the login path:

- **Company SSO** — MFA is enforced by the customer's own IdP under the customer's policy. FairSpot does not double-enforce a second factor for brokered SSO users; it trusts the authenticated assertion and any `acr`/`amr` step-up signals the customer IdP supplies.
- **FairSpot account** — MFA is enforced by FairSpot-controlled Keycloak, because these credentials live in Keycloak. Passkeys/WebAuthn are the preferred factor; OTP/TOTP is the fallback second factor; recovery codes cover lost-authenticator recovery.

Target supported factors on FairSpot-controlled Keycloak:

| Factor | Role |
| --- | --- |
| Passkey / WebAuthn | Preferred primary or second factor; phishing-resistant. |
| OTP / TOTP | Fallback second factor for users or devices that cannot use passkeys. |
| Recovery codes | One-time codes for lost-authenticator recovery; invalidated on use and regenerated. |

Verified email ownership (AUTH008) is a prerequisite for any email-based recovery or fallback factor on FairSpot-local accounts; a typed email or domain never grants tenant access by itself.

AUTH009 self-service activation (#738): a tenant user provisioned or invited as inactive stays blocked from FairSpot APIs (via the shared deactivation gate) until they prove ownership of their identity email through a one-time activation challenge. The challenge is issued from an admin/provisioning path and confirmed on an anonymous, rate-limited endpoint that trusts only the opaque challenge id plus one-time token — never a caller-supplied tenant, user, role, or email. The token is Secret: hashed at rest, never logged or returned; only its SHA-256 hash is persisted. This is the login/identity-email activation path and is separate from AUTH008B, which verifies a changed operational notification address for an already-active user. Successful activation records the identity email as verified as a Profile-persisted lifecycle fact; it does not mutate the IdP's own `emailVerified`/credentials — real IdP sync and any Keycloak credential setup are a later follow-up, not part of this gate.

Detailed Keycloak realm/client configuration for WebAuthn, OTP, and recovery codes is hosted-operator setup and lives in the private platform runbook (see [NAS Cloudflare Auth Profile](../production/nas-cloudflare-auth-profile)). Reference: [Keycloak WebAuthn / passkeys](https://www.keycloak.org/docs/latest/server_admin/#webauthn).

Backend identity scoping is unchanged by MFA: tenant, user, and role still come only from validated token claims (see [Access Control](./access-control)). A second factor strengthens login assurance; it never becomes a source of authorization identity.

### MFA / Passkey Smoke Checklist

Run against a FairSpot-controlled Keycloak profile (local, demo, or NAS) after auth-configuration changes:

- [ ] **Passkey enrollment** — a FairSpot-account user can register a passkey/WebAuthn authenticator.
- [ ] **Passkey login** — the enrolled user signs in with the passkey and receives a valid FairSpot token with the expected tenant and role claims.
- [ ] **OTP fallback** — a user without a passkey can enroll and sign in with OTP/TOTP.
- [ ] **Recovery** — a user who lost their authenticator completes recovery with a recovery code, and the used code is invalidated.
- [ ] **Privileged step-up** — an admin, auditor, or break-glass account must satisfy the stricter factor expectation before reaching privileged surfaces (see [Access Control](./access-control)).
- [ ] **SSO contrast** — a company-SSO user's second factor is enforced by the customer IdP; FairSpot does not prompt for an additional FairSpot factor.
- [ ] **Claim integrity** — after every MFA path, tenant/user/role still derive from validated claims only.
