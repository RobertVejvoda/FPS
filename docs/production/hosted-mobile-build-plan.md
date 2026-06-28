# MOB010 — Hosted Mobile Build and Store-Readiness Plan

**Status:** Plan + config scaffold. No store submission; no live developer-account artifacts created.
**Tracks:** MOB010 (issue #318).
**Priority:** P2 — must **not** block the first customer web/API deployment.
**Source of truth:** [customer-first-deployment-gap-analysis.md](./customer-first-deployment-gap-analysis.md), [mobile-device-testing.md](./mobile-device-testing.md).

---

## Principle

App Store / Google Play publication is **not** on the critical path for the first customer pilot. The pilot runs on the hosted web/API path; mobile is validated through **internal distribution** first (Expo dev client, EAS internal builds, TestFlight, Play internal testing). Public store launch is a later, separate track.

---

## 1. Hosted runtime configuration (no source edits)

The mobile app already reads its API and OIDC settings from the environment at build time — see `code/mobile/fps-mobile/app.config.js`. Resolution order per setting is:

`FPS_MOBILE_*` → `EXPO_PUBLIC_*` → the (empty) `app.json` `extra` fallback.

| Setting | Env var (preferred) | Hosted value (single-origin model) |
|---|---|---|
| API base URL | `EXPO_PUBLIC_API_BASE_URL` | `https://app.<domain>/api` |
| OIDC issuer | `EXPO_PUBLIC_AUTH_ISSUER_URL` | `https://auth.<domain>/realms/fairspot` |
| OIDC client id | `EXPO_PUBLIC_AUTH_CLIENT_ID` | `fps-mobile` (the tenant's mobile OIDC client) |
| OIDC scopes | `EXPO_PUBLIC_AUTH_SCOPES` | `openid profile email` |

`https://app.<domain>/api` matches the OPS021 single-origin routing: the web container's nginx reverse-proxies `/api/` to the Envoy gateway, and native apps are not subject to browser CORS, so the mobile app can use the same public origin. No source changes are required to point a build at a customer domain — only these environment values.

> The mobile OIDC redirect uses Expo AuthSession with the app's custom scheme, not a web URL. The current app code calls `AuthSession.makeRedirectUri({ path: 'login-callback' })`, which must be registered in Keycloak for the target build profile as part of OPS012 (public-domain auth). Confirm the exact generated URI from the build/runtime being tested before adding the allow-list entry.

---

## 2. EAS build profiles (`code/mobile/fps-mobile/eas.json`)

A minimal `eas.json` scaffold defines three profiles. The hosted URLs are supplied per build via `EXPO_PUBLIC_*` env (EAS secrets or the shell), so the file carries **no customer-specific values or secrets**:

| Profile | Distribution | Use |
|---|---|---|
| `development` | internal, dev client | Local development against a dev/hosted backend |
| `preview` | internal | Internal pilot builds (ad-hoc / internal testers) before any store |
| `production` | store build | App Store / Play submission (later track) |

Set the hosted config at build time, e.g.:

```bash
cd code/mobile/fps-mobile
EXPO_PUBLIC_API_BASE_URL=https://app.<domain>/api \
EXPO_PUBLIC_AUTH_ISSUER_URL=https://auth.<domain>/realms/fairspot \
EXPO_PUBLIC_AUTH_CLIENT_ID=fps-mobile \
EXPO_PUBLIC_AUTH_SCOPES="openid profile email" \
eas build --profile preview --platform ios   # or android
```

For repeatable cloud builds, store these as EAS environment variables / secrets per profile rather than passing them inline.

---

## 3. Internal distribution path (before any store)

| Channel | What it gives | Prerequisite |
|---|---|---|
| Expo Go / dev client | Fastest loop; JS + most native modules | None / a `development` build for native deps |
| EAS `preview` (internal) | Installable iOS/Android binaries for named testers | EAS project; iOS needs ad-hoc/enterprise provisioning or registered device UDIDs |
| iOS TestFlight | Beta distribution to internal/external testers | **Apple Developer Program account** + App Store Connect app record |
| Android internal testing | Closed track install via Play | **Google Play Console account** + an app entry |

Recommendation for the pilot: validate on **EAS `preview` internal builds** (device installs) and, once the Apple/Google accounts exist, promote to **TestFlight** and **Play internal testing**. None of these require a public store listing.

---

## 4. Store-readiness checklist (later track)

Required before App Store / Google Play **public** submission (not pilot):

- [ ] **App identifiers** — set `ios.bundleIdentifier` and `android.package` in `app.json` (currently unset). Use a stable reverse-DNS id, e.g. `net.vejvoda.fairspot` (final value is a product decision).
- [ ] **Signing** — iOS: distribution certificate + provisioning profile (EAS-managed credentials). Android: an upload keystore (EAS-managed or self-managed).
- [ ] **Apple Developer Program account** — verified org/individual; App Store Connect app record.
- [ ] **Google Play Console account** — verified; app entry with content rating.
- [ ] **Privacy policy URL** — publicly reachable.
- [ ] **Support URL / contact** — publicly reachable.
- [ ] **Data safety / privacy disclosures** — Apple Privacy "Nutrition Label" and Google Play Data Safety form (what personal data is collected/used: account identity, parking bookings).
- [ ] **Store metadata** — app name, description, keywords, category, screenshots (per device class), app icon.
- [ ] **Account verification / review evidence** — capture submission + review outcomes for the release record.

---

## 5. Costs (verify current pricing at submission time)

| Program | Fee | Official source |
|---|---|---|
| Apple Developer Program | **USD $99 / year** | https://developer.apple.com/programs/ |
| Google Play Console | **USD $25 one-time** registration | https://support.google.com/googleplay/android-developer/answer/6112435 |

EAS Build has its own free-tier limits and paid plans (separate from the store program fees); confirm current Expo pricing if cloud builds exceed the free tier.

---

## 6. What is testable before store submission

- Expo Go / dev client on a developer machine + simulator/emulator.
- EAS `preview` internal builds installed on physical iOS/Android devices.
- Full hosted flow (login via `auth.<domain>`, booking, notifications) against the NAS/Cloudflare deployment using the env config above.

Not testable without store accounts: TestFlight external testing, Play closed/open testing, and the store review process itself.

---

## 7. Remaining mobile blockers (as issues, not hidden assumptions)

| Blocker | Why | Where it belongs |
|---|---|---|
| `ios.bundleIdentifier` / `android.package` unset | Required for any EAS native/store build | Small app.json config slice (product decision on the id) |
| Mobile OIDC client + redirect not registered for the public domain | TestFlight/internal hosted login needs the real issuer plus the redirect URI generated by `AuthSession.makeRedirectUri({ path: 'login-callback' })` in Keycloak | OPS012 public-domain auth (#316) |
| Apple/Google developer accounts | Prerequisite for TestFlight / Play testing | Operator/business action (account creation + verification) |
| App icons / store screenshots / privacy + support URLs | Store metadata | Store-launch track (post-pilot) |

---

## Validation record

- Mobile config: **no source change** required for hosted URLs (existing `app.config.js` env mechanism); this slice adds `eas.json` (build config) and this plan.
- Mobile typecheck run: see PR notes. No store submission performed; no live developer-account artifacts created.
