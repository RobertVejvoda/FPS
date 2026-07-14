# AUTH006 — External IdP Broker Test Setup

**Status:** Documentation complete — implementation tracked in AUTH006 (issue #544).
**Tracks:** Issue #544
**Related docs:** [Tenant Discovery and Login Modes](../business-layer/tenant-login-modes), [NAS Cloudflare Auth Profile](./nas-cloudflare-auth-profile), [Tenant Onboarding Smoke](./tenant-onboarding-smoke)

---

## Purpose

This document explains the smallest safe path for testing Keycloak's identity-provider (IdP) broker with an external identity provider. It covers option selection, mandatory claims, Keycloak configuration steps, local validation, and rollback — without committing any secrets.

After following this guide, `ivana@greenlogistics.example` (or any user from a configured external IdP) can authenticate through the email-first FairSpot login screen (AUTH010): the user enters their work email and discovery routes them to the company-SSO path automatically. Keycloak acts as the broker and issues a FairSpot token; FPS services see only Keycloak tokens and require no code changes per tenant.

---

## How This Fits the Company SSO Path

```
User enters work email → domain discovery → routes to company SSO automatically
        │
        ▼
Keycloak login page
        │  (browser redirect)
        ▼
External IdP (Google / Entra) — authenticates user
        │  (authorization code callback)
        ▼
Keycloak receives code → validates → maps claims → issues FairSpot token
        │
        ▼
FairSpot API validates FairSpot token (trusted issuer, audience, tenant mapping)
```

The customer IdP is never a trusted issuer for FairSpot APIs. Keycloak remains the single OIDC issuer. Adding a new customer IdP broker means adding a Keycloak IdP broker entry — no FairSpot service code changes are required.

---

## Option Comparison

| | Google (free account or Google Workspace) | Microsoft Entra ID (free developer tenant) |
|---|---|---|
| **Cost** | Free Google account is sufficient | Free developer tenant at [developer.microsoft.com](https://developer.microsoft.com) |
| **Setup time** | ~15 min | ~20 min |
| **Stable `sub`** | Yes (`sub` is stable per client_id) | Yes (`oid` — use as mapped subject) |
| **Email claim** | `email` — verified | `email` / `preferred_username` — available |
| **Display name** | `name` | `name` / `display_name` |
| **Group/role claims** | Workspace only (requires paid licence for group claim) | Available on free developer tenant via app manifest |
| **Recommendation** | Best for quick solo testing without a work domain | Best for realistic enterprise demo with group/role simulation |

Both are adequate for local broker validation. Use **Google** if you want the fastest setup. Use **Entra** if you need to test role/group claim mapping.

---

## Mandatory Claims

FairSpot requires the following to be present in the Keycloak-issued token after broker mapping:

| Claim | Source | Required | Notes |
|---|---|---|---|
| `sub` | Keycloak (derived from broker subject) | Yes | Stable OIDC subject — must not change on re-login |
| `tenant_id` | Keycloak mapper | Yes | Must match the FairSpot tenant this IdP serves (e.g. `greenlogistics`) |
| `email` | External IdP → mapped | Recommended | Used for notifications; not required by the token validator itself |
| `name` | External IdP → mapped | Recommended | Used in audit actor display |
| Role claims | Keycloak realm roles | Yes | `employee`, `admin`, etc. — assigned in Keycloak per user, not from external IdP groups unless explicitly mapped |

Claims the external IdP provides (email, name) must be passed through to Keycloak and then mapped into the issued token via Protocol Mappers. Do not rely on unmapped IdP claims reaching FairSpot services.

---

## Setup: Google OAuth 2.0 Provider

### 1 — Create a Google OAuth Client

1. Open [console.cloud.google.com](https://console.cloud.google.com) → **APIs & Services** → **Credentials**.
2. Create a new project or select an existing test project.
3. Click **+ Create Credentials** → **OAuth 2.0 Client ID**.
4. Application type: **Web application**.
5. Name: `FairSpot local test`.
6. Authorised redirect URIs:
   ```
   http://localhost:8180/realms/fps-local/broker/google/endpoint
   ```
7. Click **Create**. Note the **Client ID** and **Client Secret** — store them in a password manager or environment variable. **Never commit these values.**

### 2 — Add the Google Provider in Keycloak

1. Open the Keycloak admin console at `http://localhost:8180/admin`.
2. Select the `fps-local` realm.
3. Navigate to **Identity Providers** → **Add provider** → **Google**.
4. Fill in:
   - **Client ID**: value from step 1
   - **Client Secret**: value from step 1
   - **Default Scopes**: `openid email profile`
5. Click **Save**.

### 3 — Add Protocol Mappers

After saving, open the Google provider → **Mappers** tab → **Add mapper**:

| Mapper name | Type | Source claim | Token claim | Token type |
|---|---|---|---|---|
| `google-email` | Attribute Importer | `email` | `email` | ID Token + Access Token |
| `google-name` | Attribute Importer | `name` | `name` | ID Token + Access Token |

### 4 — Add a Tenant ID Mapper

This ensures every user brokered through Google gets a `tenant_id` matching the Green Logistics tenant.

In **Identity Providers** → Google → **Mappers** → **Add mapper**:

- **Name**: `tenant-id-greenlogistics`
- **Mapper type**: Hardcoded Attribute
- **Attribute**: `tenant_id`
- **Attribute value**: `greenlogistics`
- **User attribute**: check "Store in user session"

---

## Setup: Microsoft Entra ID Provider

### 1 — Create an App Registration in Entra

1. Open [portal.azure.com](https://portal.azure.com) → **Microsoft Entra ID** → **App registrations** → **+ New registration**.
2. Name: `FairSpot local test`.
3. Supported account types: **Accounts in this organisational directory only** (or **Any Entra tenant** for cross-tenant testing).
4. Redirect URI (Web): `http://localhost:8180/realms/fps-local/broker/microsoft/endpoint`
5. Click **Register**.
6. Under **Certificates & secrets** → **+ New client secret**. Note the secret value immediately. **Never commit it.**
7. Note the **Application (client) ID** and **Directory (tenant) ID**.

### 2 — Add the Microsoft Provider in Keycloak

1. Open the Keycloak admin console → `fps-local` realm.
2. Navigate to **Identity Providers** → **Add provider** → **Microsoft**.
3. Fill in:
   - **Client ID**: Application (client) ID from step 1
   - **Client Secret**: secret value from step 1
   - **Tenant**: Directory (tenant) ID, or `common` for multi-tenant
   - **Default Scopes**: `openid email profile`
4. Click **Save**.

### 3 — Add Protocol Mappers

Same as Google — add attribute importers for `email` and `name`, and a hardcoded `tenant_id=greenlogistics` mapper.

To use the Entra stable subject (`oid`) as the Keycloak external subject, add:

- **Name**: `entra-oid-subject`
- **Mapper type**: Username Template Importer
- **Template**: `${ALIAS}.${CLAIM.oid}`

This produces a stable Keycloak username even if the Entra email changes.

---

## Secrets: Where They Live

| Secret | Stored where | Committed to git? |
|---|---|---|
| OAuth Client ID | Keycloak admin console (UI) | No |
| OAuth Client Secret | Keycloak admin console (UI) | No |
| Entra Tenant ID | Keycloak admin console (UI) | No |
| Local realm JSON | `code/infrastructure/keycloak/fps-local-realm.json` | Yes — but contains no secret values; secrets are entered after import |

The `fps-local-realm.json` realm export will not contain broker secrets if you export via the Keycloak admin API with `exportGroupsAndRoles=true&exportClients=true` — Keycloak omits client secrets from realm exports by design. Do not add secrets manually to the realm JSON.

---

## Local Validation Steps

After completing provider setup:

1. **Check the Keycloak provider appears:**
   ```
   curl -s http://localhost:8180/realms/fps-local/.well-known/openid-configuration \
     | python3 -m json.tool | grep "google\|microsoft" || echo "No broker endpoints found"
   ```

2. **Test the SSO login flow manually:**
   - Open `http://localhost:5173` (or whichever port the web app runs on).
   - Enter an email address from the configured provider (e.g. `alice@greenlogistics.example` if the domain is mapped, or any Google/Entra account for a plain broker test).
   - Click **Continue** — discovery routes the sign-in automatically (AUTH010 email-first flow).
   - You should be redirected to Google or Microsoft for authentication.
   - After authentication, you should land back on the FairSpot app.

3. **Validate the resulting token claims:**
   ```bash
   # Get a token via the password grant (requires a Keycloak-local user, not the brokered user)
   # For brokered users, inspect the token in browser DevTools → Application → Local Storage
   # or intercept with a browser extension.
   ```

4. **Verify tenant_id in the token:**
   ```bash
   # Decode the access token (base64 middle segment):
   echo "<paste_access_token_middle_segment>" | base64 -d 2>/dev/null | python3 -m json.tool | grep tenant_id
   # Expected: "tenant_id": "greenlogistics"
   ```

5. **Verify the brokered login reaches FairSpot:**
   ```bash
   curl -s -H "Authorization: Bearer <access_token>" \
     http://localhost:10000/me | python3 -m json.tool | grep -E "tenantId|roles"
   ```

---

## Remaining Blockers Before Full SSO Implementation

| Item | Status | Notes |
|---|---|---|
| Keycloak broker configuration | Manual — not automated | Client ID/secret entered by operator, not seeded by script. Must remain manual to avoid committing secrets. |
| FairSpot user provisioning for brokered users | Pending | A brokered user not in FairSpot profile facts is rejected at `GET /me` until provisioned. AUTH007+ or SCIM will address this. |
| Group/role claim mapping from external IdP | Not configured | Free Google account does not expose group claims. Entra free developer tenant supports it via app manifest, but mapping to FairSpot roles needs tenant-scoped configuration. |
| Domain-to-IdP routing in Keycloak (`kc_idp_hint`) | Pending AUTH007 | Current `login_hint` passes the email; `kc_idp_hint` would select the specific identity provider automatically. |
| Tenant-scoped IdP configuration in FairSpot | Pending | Currently, FairSpot's `TenantIdentityConfig` stores `trustedIssuer` and `audience` but not a per-tenant IdP hint for routing. |

---

## Rollback

To remove the external IdP broker configuration:

1. Open the Keycloak admin console → `fps-local` realm → **Identity Providers**.
2. Select the provider (Google or Microsoft) → click **Delete**.
3. Confirm deletion.

Keycloak will stop redirecting users to the external IdP. Users who previously authenticated through the broker and were assigned Keycloak-local accounts will not be affected — their Keycloak identities remain, but the broker link is removed.

If you want to also remove brokered user accounts from Keycloak:

1. Navigate to **Users** → search for accounts with `google.` or `microsoft.` in the username (broker-imported usernames include the provider alias as a prefix).
2. Delete each brokered account individually.

---

## See Also

- [Tenant Discovery and Login Modes](../business-layer/tenant-login-modes)
- [NAS Cloudflare Auth Profile](./nas-cloudflare-auth-profile) — public-domain Keycloak config
- [Tenant Onboarding Smoke](./tenant-onboarding-smoke) — end-to-end onboarding validation
- [Customer Integration Contract](../business-layer/customer-data-import) — full SSO claim requirements
