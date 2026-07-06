# MOB010 — Hosted Mobile Build and Store-Readiness Plan

**Status:** Plan + config scaffold. No store submission; no live developer-account artifacts created.
**Tracks:** MOB010 (issue #318, build/config mechanics), MOB012 (issue #749, launch-parity gate — supersedes the earlier "mobile is a later track" framing).
**Priority:** Launch-parity. Mobile store distribution ships with the customer launch path, not after it.
**Source of truth:** [customer-first-deployment-gap-analysis.md](./customer-first-deployment-gap-analysis.md), [mobile-device-testing.md](./mobile-device-testing.md), [release-pipeline.md](./release-pipeline.md).

---

## Principle — launch parity

Web/API and mobile ship **together** for a customer launch. App Store / Google Play publication is part of the customer launch path, not an optional later track, unless Robert explicitly approves a temporary waiver for a specific launch.

Internal and beta distribution (Expo dev client, EAS internal builds, TestFlight, Play internal/closed testing) remain **validation steps** on the way to store launch — useful pilot evidence, not the final distribution model.

### Release gate levels

| Level | What it is | When it is acceptable |
|---|---|---|
| Internal installable build | Expo dev client / EAS `preview` device installs | Development and internal pilot evidence only. |
| TestFlight / Play internal or closed testing | Beta distribution to named testers | Controlled pilot only, and only with an explicit waiver from Robert. |
| App Store / Google Play downloadable app | Public store listing | **Default target for customer launch.** Web/API and mobile ship together unless waived. |

> Store credentials, Apple/Google account operations, signing material, and private submission evidence live in the private `fairspot-platform` repository and its operator secret store — never in this public repo.

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
| `production` | store build | App Store / Play submission (default customer-launch target) |

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

These are the **validation steps** before store launch: validate on **EAS `preview` internal builds** (device installs) and, once the Apple/Google accounts exist, promote to **TestFlight** and **Play internal testing**. None of these require a public store listing — and none of them are the customer-launch distribution model, which is the public store app (see the release gate levels above).

---

## 4. Store-readiness checklist (customer-launch track)

Required before App Store / Google Play **public** submission — i.e. before customer launch, unless that launch carries an explicit mobile waiver:

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
| App icons / store screenshots / privacy + support URLs | Store metadata | Customer-launch track (required for store submission) |

---

## Validation record

- Mobile config: **no source change** required for hosted URLs (existing `app.config.js` env mechanism); this slice adds `eas.json` (build config) and this plan.
- Mobile typecheck run: see PR notes. No store submission performed; no live developer-account artifacts created.
