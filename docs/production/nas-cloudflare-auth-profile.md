# OPS012 NAS Cloudflare Auth and Gateway Profile

**Status:** Ready for operator use — complete all steps before allowing customer traffic.
**Prepared by:** Claude (FPS Implementer), 2026-05-29
**Tracks:** Issue #316
**Priority:** P0 customer-first deployment

---

## Purpose

This runbook documents how to configure Keycloak/OIDC, the Envoy API gateway, and the web and mobile applications for a public-domain FairSpot pilot deployment at `app.<domain>` and `auth.<domain>`.

This document is the companion to [OPS011 — NAS Cloudflare Deployment Profile](./nas-cloudflare-deployment-profile.md). It implements Step 7 of OPS011 (Configure OIDC for the public domain) in full. Complete OPS011 Steps 1–6 before following this document.

No secrets or live credentials are committed here. All values specific to the operator's domain are written as `<domain>`, `<realm>`, or similar placeholders. See [Section 7 — Values Robert must replace](#section-7--values-robert-must-replace) for the complete replacement table.

---

## Section 1 — Keycloak Public-Domain Configuration

### Background

The local development realm (`fps-local`) has `accessTokenLifespan: 3600` (one hour) and all redirect URIs pointing to `localhost`. Before the pilot, the realm must be updated or a dedicated pilot realm created so that:

- The OIDC issuer resolves to `https://auth.<domain>/realms/<realm>`.
- Token lifetimes follow security-model minimums (15 min access, 30 min refresh).
- Redirect URIs and web origins reference only `https://app.<domain>` for pilot clients.
- The Keycloak admin console is not reachable through the Cloudflare tunnel.

### Option A — Update the existing `fps-local` realm (simpler)

Use this option if the NAS hosts only the pilot and no local development runs against the same Keycloak instance.

### Option B — Create a dedicated `fps-pilot` realm (recommended)

Export the `fps-local` realm as a starting point, import it as `fps-pilot`, and apply the changes below to that realm only. This keeps the local development clients (`fps-web-dev`, `fps-mobile-dev`) untouched in `fps-local` and gives the pilot its own namespace and clean client set.

> The remaining steps in this section refer to the realm you choose as `<realm>`. The recommended value is `fps-pilot`.

---

### 1.1 — Update the Frontend URL

The Frontend URL controls the `iss` claim in all tokens issued by the realm.

1. Log in to the Keycloak admin console at `http://localhost:8080/admin` (NAS local access only — do not use the public `auth.<domain>` hostname for admin operations).
2. Select the `<realm>` realm from the realm dropdown.
3. Navigate to **Realm settings** → **General**.
4. Set **Frontend URL** to `https://auth.<domain>`.
5. Click **Save**.

After this change, the OIDC discovery document will be served at:

```
https://auth.<domain>/realms/<realm>/.well-known/openid-configuration
```

and all tokens will carry `"iss": "https://auth.<domain>/realms/<realm>"`.

---

### 1.2 — Configure the `fps-web` client

The current realm JSON defines `fps-web-dev` (localhost-only). For the pilot, either rename this client to `fps-web` or create a new client with the following settings.

> `fps-web-dev` should be excluded from the pilot realm or left disabled. Do not add public-domain redirect URIs to the dev client.

1. In the `<realm>` realm, navigate to **Clients**.
2. Click **Create client** (or select the existing client to edit).
3. Set **Client ID** to `fps-web`.
4. Set **Name** to `FairSpot Web`.
5. Confirm **Client authentication** is **OFF** (public client — no client secret).
6. Enable **Standard flow** (Authorization Code). Disable all other flows.
7. Under **Valid redirect URIs**, enter: `https://app.<domain>/*`
8. Remove all `http://localhost:*` entries if copied from the dev client.
9. Under **Web origins**, enter: `https://app.<domain>`
10. Under **Advanced** → **Fine-grained OpenID Connect configuration**, set **Access token lifespan** to `900` (15 minutes).
11. Set `post.logout.redirect.uris` to `https://app.<domain>/`.
12. Click **Save**.

| Setting | Value |
|---|---|
| Client ID | `fps-web` |
| Client type | Public (no secret) |
| Standard flow | Enabled |
| Valid redirect URIs | `https://app.<domain>/*` |
| Web origins | `https://app.<domain>` |
| Post-logout redirect URI | `https://app.<domain>/` |
| Access token lifespan | `900` (15 min) |
| PKCE method | `S256` |

---

### 1.3 — Configure the `fps-mobile-android` and `fps-mobile-ios` clients

Mobile apps use Authorization Code + PKCE with a custom URI scheme. Create one client per platform or a single shared `fps-mobile` client.

1. In the `<realm>` realm, navigate to **Clients** → **Create client**.
2. Set **Client ID** to `fps-mobile-android` (repeat for `fps-mobile-ios`, or use `fps-mobile` for a shared client).
3. Confirm **Client authentication** is **OFF** (public client).
4. Enable **Standard flow**. Disable all other flows.
5. Under **Valid redirect URIs**, add both:
   - `fps://auth/callback`
   - `https://app.<domain>/*` (for future web-view fallback)
6. Under **Web origins**, enter `https://app.<domain>`.
7. Set **Access token lifespan** to `900`.
8. Under **Advanced**, set `pkce.code.challenge.method` to `S256`.
9. Click **Save**.

> The custom scheme `fps://auth/callback` must match exactly what the Expo/React Native app sends in its `redirect_uri` parameter. See Section 4 for the mobile app configuration.

| Setting | Value |
|---|---|
| Client ID | `fps-mobile-android` / `fps-mobile-ios` / `fps-mobile` |
| Client type | Public (no secret) |
| Standard flow | Enabled |
| Valid redirect URIs | `fps://auth/callback`, `https://app.<domain>/*` |
| Web origins | `https://app.<domain>` |
| Access token lifespan | `900` (15 min) |
| PKCE method | `S256` |

---

### 1.4 — Token and cookie settings

1. In **Realm settings** → **Tokens**, confirm or set:
   - **Access token lifespan**: `900` (15 min) — this is the realm default; client-level overrides in 1.2 and 1.3 take precedence.
   - **SSO session max lifespan**: `1800` (30 min) — controls how long the session (and thus refresh capability) persists.
2. In **Realm settings** → **Sessions**, confirm **SSO session idle** is appropriate for the pilot (recommend `1800`).

Cookie settings are controlled by Keycloak's internal session handling. When `Frontend URL` is set to `https://auth.<domain>` and all traffic flows through the Cloudflare-terminated HTTPS tunnel, Keycloak will set cookies with `Secure=true`. Ensure the following in Keycloak realm settings:

| Cookie attribute | Required value | Where to confirm |
|---|---|---|
| `Secure` | `true` | Automatic when Frontend URL is `https://` |
| `SameSite` | `Strict` or `Lax` | Set in **Realm settings** → **Security defenses** → **Headers** if configurable, or accept Keycloak default |
| `HttpOnly` | `true` | Keycloak default for session cookies |

---

### 1.5 — Admin console access

The Keycloak admin console must **not** be published as a public Cloudflare hostname.

- The `auth.<domain>` tunnel entry points to `http://keycloak:8080`, which includes the full Keycloak application.
- Add a **Cloudflare WAF custom rule** (also covered in SEC010/issue #315) to block requests matching `http.request.uri.path contains "/admin"` on the `auth.<domain>` hostname.
- Alternatively, configure Cloudflare Zero Trust Access on a separate `ops.<domain>` hostname restricted to named operators if remote admin access is needed.
- For routine admin operations, access Keycloak directly at `http://localhost:8080/admin` from the NAS or over an SSH tunnel.

---

### 1.6 — Protocol mappers and identity claim requirements

All FairSpot services extract identity from JWT claims. The pilot realm must include the following protocol mappers on every pilot client (these are already present in `fps-local` and should be carried forward):

| Claim | Mapper type | Source | Required by |
|---|---|---|---|
| `tenant_id` | `oidc-usermodel-attribute-mapper` | User attribute `tenant_id` | All services — tenant scoping |
| `sub` | Built-in | Keycloak user UUID | All services — user identity |
| `roles` | `oidc-usermodel-realm-role-mapper` | Realm roles | `TenantRoleMapping` in each service |
| `aud` | `oidc-audience-mapper` | Client ID | Service audience validation |

To verify mappers are present:

1. In the `<realm>` realm, navigate to **Clients** → select `fps-web` → **Client scopes** tab.
2. Confirm `tenant_id`, `roles`, and the audience mapper appear in the assigned or dedicated scope.
3. Use the **Evaluate** sub-tab, enter `employee1`, and confirm the generated access token contains `tenant_id`, `sub`, and `roles` claims.

FairSpot role values expected by `TenantRoleMapping`:

| Realm role | FairSpot role constant | Typical user |
|---|---|---|
| `employee` | `employee` | employee1, employee2, employee3 |
| `hr_manager` | `hr_manager` | hr-admin |
| `admin` | `admin` | tenant-admin |
| `report_viewer` | `report_viewer` | report-viewer |
| `auditor` | `auditor` | auditor |

Tenant/user/role claims must always come from the validated JWT. Never accept `tenant_id` or role values from request bodies, query strings, or headers from untrusted callers.

---

## Section 2 — Envoy Gateway Public-Domain Configuration

### Background

The development `envoy.yaml` allows CORS from `http://localhost:5200` only. In the pilot, the web app is served at `https://app.<domain>`, so browsers will send preflight requests with `Origin: https://app.<domain>`. These will be rejected unless the CORS configuration is updated.

The Envoy listener continues to bind to `0.0.0.0:10000` internally. Cloudflare Tunnel routes `app.<domain>` to `http://envoy-proxy:10000` inside the Docker network — no listener port change is needed.

### 2.1 — What changes

The only required change to `envoy.yaml` for the pilot is in the `allow_origin_string_match` field under `cors`:

**In `envoy.yaml` (development):**
```yaml
cors:
  allow_origin_string_match:
    - exact: "http://localhost:5200"
```

**In `envoy-public.yaml` (pilot):**
```yaml
cors:
  allow_origin_string_match:
    - exact: "https://app.<domain>"
```

Replace `<domain>` with your actual domain. The full `envoy-public.yaml` file is at `code/infrastructure/envoy/envoy-public.yaml`.

### 2.2 — Deployment approach

Use `envoy-public.yaml` as the config file for the pilot Envoy container instead of `envoy.yaml`. In `docker-compose.yaml` (or an override file for NAS), update the Envoy service volume mount:

```yaml
services:
  envoy-proxy:
    volumes:
      # Development (localhost CORS):
      # - ./envoy/envoy.yaml:/etc/envoy/envoy.yaml:ro
      # Pilot (public-domain CORS) — uncomment and replace <domain>:
      - ./envoy/envoy-public.yaml:/etc/envoy/envoy.yaml:ro
```

Do not modify `envoy.yaml` itself — keep it as the localhost development baseline. The `envoy-public.yaml` file is the NAS pilot overlay.

### 2.3 — Listener and cluster addresses

The `envoy-public.yaml` retains the same cluster backend addresses as `envoy.yaml`. On the NAS, all .NET services run as containers on the same Docker network. Change `host.docker.internal` addresses to the container service names if the services run inside Docker (rather than on the host). For the NAS Docker Compose deployment, update each cluster's `socket_address` to use the Docker service name instead of `host.docker.internal`:

| Cluster | Development address | NAS Docker address |
|---|---|---|
| `fps-identity` | `host.docker.internal:5192` | `fps-identity:5192` (if containerised) |
| `fps-booking` | `host.docker.internal:5131` | `fps-booking:5131` (if containerised) |
| `fps-notification` | `host.docker.internal:5157` | `fps-notification:5157` (if containerised) |
| `fps-profile` | `host.docker.internal:5197` | `fps-profile:5197` (if containerised) |
| `fps-reporting` | `host.docker.internal:5171` | `fps-reporting:5171` (if containerised) |
| `fps-audit` | `host.docker.internal:5161` | `fps-audit:5161` (if containerised) |
| `fps-configuration` | `host.docker.internal:5141` | `fps-configuration:5141` (if containerised) |
| `fps-customer` | `host.docker.internal:5181` | `fps-customer:5181` (if containerised) |

If .NET services still run on the NAS host (not in Docker), keep `host.docker.internal` and ensure the NAS Linux Docker host has `--add-host=host.docker.internal:host-gateway` set on the Envoy container.

---

## Section 3 — Web App Runtime Configuration

### Background

The web app (`code/web/fps-web`) reads runtime configuration from `public/config.json`. In development this file points to `localhost`. For the pilot, these values must be changed to the public domain. No secrets appear in these values — they are public OIDC client configuration.

### 3.1 — `public/config.json` values for the pilot

| Key | Development value | Pilot value |
|---|---|---|
| `apiBaseUrl` | `http://localhost:10000` | `https://app.<domain>` |
| `oidc.authority` | `http://localhost:8180/realms/fps-local` | `https://auth.<domain>/realms/<realm>` |
| `oidc.clientId` | `fps-web-dev` | `fps-web` |
| `oidc.redirectUri` | `http://localhost:5200/auth/callback` | `https://app.<domain>/auth/callback` |
| `oidc.postLogoutRedirectUri` | `http://localhost:5200/` | `https://app.<domain>/` |
| `oidc.scopes` | `openid profile email` | `openid profile email` (unchanged) |
| `devTokenFallbackEnabled` | `false` | `false` (must remain false in pilot) |

### 3.2 — Pilot `config.json` (complete)

```json
{
  "apiBaseUrl": "https://app.<domain>",
  "oidc": {
    "authority": "https://auth.<domain>/realms/<realm>",
    "clientId": "fps-web",
    "scopes": "openid profile email",
    "redirectUri": "https://app.<domain>/auth/callback",
    "postLogoutRedirectUri": "https://app.<domain>/"
  },
  "branding": {
    "productName": "FairSpot",
    "tenantName": "Demo Company",
    "logoUrl": "/brand/fairspot-app-icon.svg",
    "primaryColor": "#2f7d3f",
    "accentColor": "#43b75a"
  },
  "devTokenFallbackEnabled": false
}
```

Replace `<domain>` and `<realm>` before deploying. Do not commit a `config.json` containing the live domain — distribute it as an operator-supplied runtime file or inject it via the container build process outside of source control.

### 3.3 — Notes

- `devTokenFallbackEnabled` must be `false` in any pilot or production deployment. This flag enables a development-only bearer token bypass that has no place in an internet-facing environment.
- All values in `config.json` are sent to the browser and are not secret. The `fps-web` client is a public OIDC client with no client secret.
- If the web app is served from a CDN or object-storage static host, ensure the `config.json` file is updated at the serving location, not just in the repository.

---

## Section 4 — Mobile App Runtime Configuration

### Background

The Expo/React Native mobile app uses Authorization Code + PKCE. There is no client secret. The app reads its configuration from Expo public environment variables (prefixed `EXPO_PUBLIC_`), which are embedded at build time and safe to appear in client bundles.

### 4.1 — Environment variable values for the pilot

| Variable | Development value | Pilot value |
|---|---|---|
| `EXPO_PUBLIC_API_BASE_URL` | `http://localhost:10000` | `https://app.<domain>` |
| `EXPO_PUBLIC_AUTH_URL` | `http://localhost:8180` | `https://auth.<domain>` |
| `EXPO_PUBLIC_OIDC_CLIENT_ID` | `fps-mobile-dev` | `fps-mobile-android` or `fps-mobile` |
| `EXPO_PUBLIC_OIDC_REDIRECT_URI` | (local scheme) | `fps://auth/callback` |
| `EXPO_PUBLIC_OIDC_AUTHORITY` | `http://localhost:8180/realms/fps-local` | `https://auth.<domain>/realms/<realm>` |

### 4.2 — Pilot `.env` file for Expo build

```dotenv
EXPO_PUBLIC_API_BASE_URL=https://app.<domain>
EXPO_PUBLIC_AUTH_URL=https://auth.<domain>
EXPO_PUBLIC_OIDC_CLIENT_ID=fps-mobile
EXPO_PUBLIC_OIDC_AUTHORITY=https://auth.<domain>/realms/<realm>
EXPO_PUBLIC_OIDC_REDIRECT_URI=fps://auth/callback
```

This file is not committed to source control. Supply it at build time or via the Expo secrets facility.

### 4.3 — Redirect URI scheme registration

The custom URI scheme `fps://auth/callback` must be registered in the Keycloak client (see Section 1.3) exactly as it appears in the mobile app. Keycloak performs exact-string matching on redirect URIs for public clients.

| Platform | Redirect URI | Registration location |
|---|---|---|
| Android | `fps://auth/callback` | Keycloak client → Valid redirect URIs |
| iOS | `fps://auth/callback` | Keycloak client → Valid redirect URIs |
| Expo Go (dev only) | `exp://*` | `fps-mobile-dev` client in `fps-local` realm only |

### 4.4 — Notes

- Mobile uses Authorization Code + PKCE (`S256`). No client secret is stored in or distributed with the mobile app.
- `EXPO_PUBLIC_` variables are embedded in the JavaScript bundle and visible to users who inspect the bundle. This is intentional and expected for public OIDC client configuration.
- If the pilot uses a single `fps-mobile` client for both platforms, both `fps-mobile-android` and `fps-mobile-ios` Client IDs should redirect to it, or use one client with both platform redirect URIs registered.

---

## Section 5 — Service Auth Configuration

### Background

Each .NET FairSpot service validates JWT tokens against the OIDC issuer and audience configured in `Auth:Authority` and `Auth:Audience`. These values are supplied via environment variables at runtime — they are not hardcoded in source and do not contain secrets.

### 5.1 — Required environment variables per service

Set these values in the Docker Compose environment section (or `.env.nas`) for each service. Values below use `<domain>` and `<realm>` as placeholders.

| Service | Env var | Example value |
|---|---|---|
| `fps-identity` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-identity` | `Auth__Audience` | `fps-web` |
| `fps-booking` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-booking` | `Auth__Audience` | `fps-web` |
| `fps-profile` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-profile` | `Auth__Audience` | `fps-web` |
| `fps-notification` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-notification` | `Auth__Audience` | `fps-web` |
| `fps-audit` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-audit` | `Auth__Audience` | `fps-web` |
| `fps-reporting` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-reporting` | `Auth__Audience` | `fps-web` |
| `fps-configuration` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-configuration` | `Auth__Audience` | `fps-web` |
| `fps-customer` | `Auth__Authority` | `https://auth.<domain>/realms/<realm>` |
| `fps-customer` | `Auth__Audience` | `fps-web` |

> The double-underscore (`__`) separator is the .NET environment variable convention for nested configuration keys (`Auth:Authority` becomes `Auth__Authority`).

> The `Auth__Audience` value must match the `aud` claim in the JWT as configured by the Keycloak audience mapper. If you add a dedicated service audience mapper per client, update these values to match. For the pilot, `fps-web` is used as the shared audience across clients.

### 5.2 — Claim enforcement rules

- `tenant_id` must come from the validated JWT claim. It must never be accepted from request bodies, query strings, or client-supplied headers.
- `sub` (user identity) must come from the validated JWT.
- Role values must come from the `roles` JWT claim, mapped through `TenantRoleMapping` (`ConfiguredTenantRoleMapper`) in each service's configuration section.
- Services fail closed: if required claims are missing or the token cannot be validated, the request is rejected with 401.

### 5.3 — `TenantRoleMapping` configuration

Each service has a `TenantRoleMapping` configuration section that maps realm-role strings from the JWT to FairSpot internal role constants. Confirm this section is present and correct in each service's `appsettings.json` or environment override for the pilot realm.

---

## Section 6 — Smoke Checklist

Run these checks after completing Sections 1–5, before allowing customer traffic. All checks are from an external machine (not the NAS) unless otherwise noted.

| # | Check | Command / URL | Expected result |
|---|---|---|---|
| 1 | OIDC discovery reachable | `curl -s https://auth.<domain>/realms/<realm>/.well-known/openid-configuration \| jq .issuer` | `"https://auth.<domain>/realms/<realm>"` |
| 2 | App gateway reachable | `curl -I https://app.<domain>/openapi/v1.json` | HTTP 200 or 401 (gateway up; auth may block) |
| 3 | CORS preflight accepted | `curl -I -X OPTIONS https://app.<domain>/me -H "Origin: https://app.<domain>" -H "Access-Control-Request-Method: GET"` | HTTP 200; response includes `Access-Control-Allow-Origin: https://app.<domain>` |
| 4 | Employee login via web | Open `https://app.<domain>` in browser → log in as `employee1` | Redirected to Keycloak at `https://auth.<domain>`, login succeeds, redirected back to `https://app.<domain>/auth/callback` |
| 5 | `/me` returns correct claims | After login in step 4, trigger `GET https://app.<domain>/me` | Returns `tenantId: "demo"`, `roles: ["employee"]` |
| 6 | Booking creation | `POST https://app.<domain>/bookings` with valid JWT | HTTP 201 Accepted |
| 7 | Keycloak admin blocked | `curl -I https://auth.<domain>/admin` | HTTP 403 or connection refused (WAF rule or not published) |
| 8 | CORS from localhost blocked | `curl -I -X OPTIONS https://app.<domain>/me -H "Origin: http://localhost:5200"` | HTTP 200 response must NOT include `Access-Control-Allow-Origin: http://localhost:5200` |
| 9 | Internal MongoDB not exposed | From external machine: `curl -v https://<domain>:27017` | Connection refused or timeout |
| 10 | Token audience correct | Decode JWT from step 4 | `aud` claim contains `fps-web`; `iss` is `https://auth.<domain>/realms/<realm>` |

---

## Section 7 — Values Robert Must Replace

Replace every placeholder below with the actual value for the pilot environment before following any step in this document.

| Placeholder | What it represents | Where to set it |
|---|---|---|
| `<domain>` | Actual domain registered and managed in Cloudflare (e.g. `fairspot.example.com`) | Cloudflare DNS, Keycloak Frontend URL, `config.json`, `envoy-public.yaml`, `.env.nas`, service env vars |
| `<realm>` | Keycloak realm name for the pilot. Default in source is `fps-local`. Recommended: rename to `fps-pilot` for isolation. | Keycloak realm settings; all OIDC authority URLs |
| `fps-web` | OIDC client ID for the web app. Can be kept as `fps-web` or customised. | Keycloak client settings, `config.json`, service `Auth__Audience` |
| `fps-mobile` | OIDC client ID for the mobile app. Can be `fps-mobile`, `fps-mobile-android`, `fps-mobile-ios`. | Keycloak client settings, mobile `.env` |
| `fps://auth/callback` | Custom URI scheme for mobile OAuth redirect. Must match Expo app registration. | Keycloak client → Valid redirect URIs, mobile app config |

### OIDC client summary table

| Client | Platform | Public | Redirect URI | Web origin |
|---|---|---|---|---|
| `fps-web` | Web browser | Yes (no secret) | `https://app.<domain>/auth/callback` | `https://app.<domain>` |
| `fps-mobile` | Android / iOS | Yes (no secret) | `fps://auth/callback` | `https://app.<domain>` |
| `fps-web-dev` | Local dev only | Yes | `http://localhost:5200/auth/callback` | Keep in `fps-local` only; exclude from pilot realm |
| `fps-mobile-dev` | Expo Go dev | Yes | `exp://*` | Keep in `fps-local` only; exclude from pilot realm |

---

## Cross-Reference

| Document | Relationship |
|---|---|
| [nas-cloudflare-deployment-profile.md](./nas-cloudflare-deployment-profile.md) | OPS011 — Steps 1–6 must be completed first. This document implements OPS011 Step 7 in full. |
| [client-production-handoff.md](./client-production-handoff.md) | OPS003 — Defines client identity integration requirements and `TenantRoleMapping` contract. |
| [security-model.md](../security/security-model.md) | Defines data classification, SSO-first integration requirements, and claim enforcement rules referenced in Sections 1 and 5. |
| SEC010 (issue #315) | WAF and rate-limiting policy — must be completed alongside or before this document for pilot go-live. |
| OPS013 (issue #314) | Hosted smoke/readiness evidence — end-to-end automated smoke check for the public domain. |

---

## Document Change Log

| Date | Author | Change |
|---|---|---|
| 2026-05-29 | Claude | Initial auth and gateway profile for issue #316 |
