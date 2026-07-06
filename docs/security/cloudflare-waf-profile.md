# SEC010 Cloudflare WAF and Origin Hardening Profile

> **Public contract (#670/#684):** this page keeps public WAF/origin-hardening expectations and template guidance. Cloudflare account details, environment-specific rules, evidence captures, and operator procedures belong in the private `fairspot-platform` repository; the [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary.md) tracks that split.

**Status:** Ready for operator use — plan-dependent features noted throughout.
**Prepared by:** Claude (FPS Implementer), 2026-05-29
**Tracks:** Issue #315
**Priority:** P0 customer-first deployment

---

## Purpose

This document defines the Cloudflare Web Application Firewall (WAF) and NAS origin hardening policy for the FairSpot public-domain pilot. It covers custom WAF rules, managed rule sets, rate limiting, Cloudflare Access policies for operator surfaces, NAS firewall posture, and TLS settings.

This is a **documentation and configuration template only**. No live Cloudflare account changes are described as already applied. The operator must apply each section using the Cloudflare dashboard or Terraform before allowing customer traffic.

Cross-references:

- NAS deployment runbook: [`docs/production/nas-cloudflare-deployment-profile.md`](../production/nas-cloudflare-deployment-profile.md) (OPS011)
- Gap register entry: [`docs/security/gap-register.md`](./gap-register.md) — Network Security section
- Network security baseline: [`docs/security/network-security.md`](./network-security.md)

---

## Public hostnames in scope

| Hostname | Upstream | Purpose |
|---|---|---|
| `app.<domain>` | Envoy API gateway on `:10000` | FairSpot API — authenticated users |
| `auth.<domain>` | Keycloak on `:8080` | OIDC login, token, and realm endpoints |
| `ops.<domain>` | Grafana on `:3000` | Operator observability — behind Access only |

Replace `<domain>` with the operator's actual domain throughout this document.

---

## Plan-dependent features

Before configuring any rules, confirm which Cloudflare plan is active. Features vary significantly by plan.

| Feature | Free | Pro | Business | Enterprise |
|---|---|---|---|---|
| Cloudflare managed rules / WAF managed rulesets | No in current Free setup | Yes | Yes | Yes |
| OWASP Core Rule Set (managed rules) | No | Yes | Yes | Yes |
| Custom WAF rules (up to 5) | Yes (5 rules) | Yes (20 rules) | Yes (100 rules) | Yes (unlimited) |
| Rate limiting rules (counting-based) | No | Yes | Yes | Yes |
| Advanced rate limiting (custom counting fields) | No | No | Yes | Yes |
| Cloudflare Access (Zero Trust) | Yes (up to 50 users) | Yes | Yes | Yes |
| Bot Fight Mode (basic) | Yes | Yes | Yes | Yes |
| Super Bot Fight Mode | No | Yes | Yes | Yes |
| Always Use HTTPS | Yes | Yes | Yes | Yes |
| HSTS | Yes | Yes | Yes | Yes |
| Full (strict) TLS | Yes | Yes | Yes | Yes |

**Current Release 1 baseline:** Free plan. Use custom WAF rules and TLS settings now. Treat managed WAF rulesets, OWASP managed rules, and edge rate limiting as upgrade gaps until the plan is upgraded.

**Recommended customer-facing plan:** Pro or higher, because it unlocks managed WAF rules and rate limiting. Do not mark the managed-rules or rate-limit checks as complete on a Free plan.

---

## 1. WAF custom rules

Custom WAF rules block or challenge requests before they reach the NAS. Configure these in the Cloudflare dashboard under **Security** → **WAF** → **Custom rules** for the zone, or via the Cloudflare API / Terraform `cloudflare_ruleset` resource.

Use `starts_with()` and `contains` in the expressions below. The regex `matches` operator requires Cloudflare Business or WAF Advanced and should not be used in the baseline Release 1 profile.

### 1.1 Block internal paths on `app.<domain>`

The following paths must never be reachable from the Internet. They expose Dapr sidecar APIs, internal health checks, diagnostic endpoints, and documentation surfaces.

**Single-origin note (Release 1):** `app.<domain>` serves the SPA and reverse-proxies the API under `/api/` to the Envoy gateway. Cloudflare evaluates the **browser-facing** path *before* nginx strips the `/api` prefix, so each internal path must be blocked in **both** its root form and its `/api/`-prefixed form. The SEC011 hosted smoke enforces exactly this: it checks `/metrics` at the root origin and `/api/{openapi,swagger,v1.0/*}` at the API base.

**Rule name:** `FPS — Block internal paths on app`

**Action:** Block (returns HTTP 403)

**Cloudflare Ruleset Language expression:**

```
(
  http.host eq "app.REPLACE_WITH_DOMAIN"
  and (
    starts_with(http.request.uri.path, "/metrics")
    or starts_with(http.request.uri.path, "/api/metrics")
    or starts_with(http.request.uri.path, "/dapr/")
    or starts_with(http.request.uri.path, "/api/dapr/")
    or starts_with(http.request.uri.path, "/v1.0/")
    or starts_with(http.request.uri.path, "/api/v1.0/")
    or starts_with(http.request.uri.path, "/healthz")
    or starts_with(http.request.uri.path, "/api/healthz")
    or starts_with(http.request.uri.path, "/swagger")
    or starts_with(http.request.uri.path, "/api/swagger")
    or starts_with(http.request.uri.path, "/openapi")
    or starts_with(http.request.uri.path, "/api/openapi")
    or starts_with(http.request.uri.path, "/admin")
    or starts_with(http.request.uri.path, "/api/admin")
    or starts_with(http.request.uri.path, "/_")
    or starts_with(http.request.uri.path, "/api/_")
  )
)
```

Replace `REPLACE_WITH_DOMAIN` with the operator's domain (e.g. `app.example.com`).

The `/api/*` clauses match the single-origin browser path; the root clauses keep the rule correct for any direct-origin or non-single-origin access. Legitimate API calls (`/api/me`, `/api/bookings`, `/api/health/identity`, `/api/draws/...`) are unaffected — none start with a blocked diagnostic prefix (note `/healthz` ≠ `/api/health/...`).

**Paths blocked and rationale:**

| Path pattern | Reason to block |
|---|---|
| `/metrics`, `/api/metrics` | Prometheus scrape endpoint — internal only |
| `/dapr/*`, `/api/dapr/*` | Dapr HTTP sidecar API — never exposed publicly |
| `/v1.0/*`, `/api/v1.0/*` | Dapr API prefix — overlaps with Dapr invoke paths |
| `/healthz`, `/api/healthz` | Kubernetes/Envoy health endpoint — internal probe only |
| `/swagger`, `/api/swagger` | OpenAPI UI — should not be public in production |
| `/openapi`, `/api/openapi` | OpenAPI schema endpoint |
| `/admin`, `/api/admin` | Catch-all admin prefix (Keycloak admin is on `auth.<domain>` — see 1.2) |
| `/_*`, `/api/_*` | Framework internal routes (ASP.NET, Envoy internals) |

> **Note:** If the operator needs to expose `/openapi` (or `/api/openapi`) for developer portal purposes, remove those clauses and apply authentication via Cloudflare Access instead.

### 1.2 Block Keycloak admin paths on `auth.<domain>`

The Keycloak admin console must never be accessible from the public Internet. If operator admin access is required, it must go through an Access-protected `ops.<domain>` tunnel endpoint or local access only.

**Rule name:** `FPS — Block Keycloak admin paths on auth`

**Action:** Block (returns HTTP 403)

**Cloudflare Ruleset Language expression:**

```
(
  http.host eq "auth.REPLACE_WITH_DOMAIN"
  and (
    starts_with(http.request.uri.path, "/auth/admin")
    or starts_with(http.request.uri.path, "/admin")
    or starts_with(http.request.uri.path, "/metrics")
    or (
      starts_with(http.request.uri.path, "/realms/")
      and http.request.uri.path contains "/account/applications"
    )
  )
)
```

**Paths blocked and rationale:**

| Path pattern | Reason to block |
|---|---|
| `/auth/admin/*` | Keycloak admin REST API — operator only |
| `/admin/*` | Keycloak admin console UI |
| `/realms/*/account/applications` | Account application management — optional; block if not used by end users |
| `/metrics` | Keycloak Prometheus metrics endpoint — internal only |

### 1.3 Rule ordering

In the Cloudflare dashboard, custom rules are evaluated in priority order. Set the internal-path blocking rules at the highest priority (lowest priority number) so they fire before any other rule.

Suggested priority assignments:

| Priority | Rule name |
|---|---|
| 1 | `FPS — Block internal paths on app` |
| 2 | `FPS — Block Keycloak admin paths on auth` |
| 10+ | Rate limiting rules (see section 3) |
| 100+ | Any allow-list overrides for known operator IPs |

---

## 2. Managed rules and OWASP

### 2.1 Cloudflare managed ruleset

Enable the **Cloudflare Managed Ruleset** only if the active plan exposes it. In the current Free plan setup this is not available, so record it as a Release 1 upgrade gap rather than a completed control.

**Dashboard path:** Security → WAF → Managed rules → Deploy Cloudflare Managed Ruleset

When available, default action for most rules in this ruleset is **Managed Challenge** (browser integrity check). Review the ruleset overrides once customer traffic is flowing and tune any rules generating false positives.

### 2.2 OWASP Core Rule Set (Pro/Business/Enterprise only)

If the domain is on a Pro plan or above, also deploy the **Cloudflare OWASP Core Ruleset**.

**Dashboard path:** Security → WAF → Managed rules → Deploy OWASP Core Ruleset

Recommended initial settings:

| Setting | Recommended value |
|---|---|
| Paranoia level | PL1 (default) — increase to PL2 after baseline |
| Score threshold | Medium (score ≥ 25 triggers action) |
| Action | Managed Challenge |

Start at PL1/Medium to avoid false positives on the FairSpot API. After one week of customer traffic, review the WAF analytics and increase paranoia level if the false-positive rate is acceptable.

> **Free plan:** OWASP managed rules are not available. Document the gap in the operator's risk register and plan an upgrade path to Pro before processing sensitive personal data.

---

## 3. Rate limiting

Rate limiting rules count requests matching a condition and block or challenge the source when a threshold is exceeded. **Rate limiting with counting rules requires Cloudflare Pro or above.**

All thresholds below are conservative defaults tuned for a pilot deployment. The operator should review and adjust based on actual traffic patterns after go-live. Thresholds that are too tight will degrade the user experience; thresholds that are too loose will not protect against credential-stuffing or abuse.

Configure rate limiting rules under **Security** → **WAF** → **Rate limiting rules**.

### 3.1 Login and token endpoints

**Rule name:** `FPS — Rate limit login and token`

| Field | Value |
|---|---|
| Match on | `http.host eq "auth.REPLACE_WITH_DOMAIN" and http.request.uri.path contains "/protocol/openid-connect/token"` |
| Also match | `http.host eq "app.REPLACE_WITH_DOMAIN" and http.request.method eq "POST" and starts_with(http.request.uri.path, "/token")` |
| Counting dimension | IP address |
| Threshold | 5 requests |
| Period | 10 seconds |
| Action | Block (HTTP 429) |
| Duration | 60 seconds |

**Rationale:** The OIDC token endpoint is the primary credential-stuffing target. 5 requests per 10 seconds per IP permits a single automated retry on login error but blocks sustained brute-force attempts.

### 3.2 Booking submission

**Rule name:** `FPS — Rate limit booking submission`

| Field | Value |
|---|---|
| Match on | `http.host eq "app.REPLACE_WITH_DOMAIN" and http.request.method eq "POST" and starts_with(http.request.uri.path, "/bookings")` |
| Counting dimension (Pro) | IP address |
| Counting dimension (Business+) | Custom header `CF-Connecting-IP` correlated with `Authorization` JWT subject (requires advanced rate limiting) |
| Threshold | 10 requests per IP per minute (Pro) — or 3 per authenticated user per minute (Business+) |
| Action | Block (HTTP 429) |
| Duration | 60 seconds |

> **Business plan note:** Advanced rate limiting allows counting by JWT claim or cookie value. If the plan supports it, prefer counting per authenticated user rather than per IP so that shared office NAT does not block legitimate concurrent users.

### 3.3 Draw trigger

**Rule name:** `FPS — Rate limit draw trigger`

| Field | Value |
|---|---|
| Match on | `http.host eq "app.REPLACE_WITH_DOMAIN" and http.request.method eq "POST" and starts_with(http.request.uri.path, "/draws")` |
| Counting dimension | IP address |
| Threshold | 2 requests per minute |
| Action | Block (HTTP 429) |
| Duration | 120 seconds |

Draw triggers are admin-only operations. A threshold of 2 per minute per IP accommodates a single operator retry while preventing automated triggering.

### 3.4 HR cancellation

**Rule name:** `FPS — Rate limit HR cancellation`

| Field | Value |
|---|---|
| Match on | `http.host eq "app.REPLACE_WITH_DOMAIN" and http.request.method eq "POST" and starts_with(http.request.uri.path, "/bookings/") and http.request.uri.path contains "/cancel"` |
| Counting dimension | IP address |
| Threshold | 5 requests per minute |
| Action | Block (HTTP 429) |
| Duration | 60 seconds |

### 3.5 Import endpoints

**Rule name:** `FPS — Rate limit import`

| Field | Value |
|---|---|
| Match on | `http.host eq "app.REPLACE_WITH_DOMAIN" and http.request.method eq "POST" and (starts_with(http.request.uri.path, "/import") or http.request.uri.path contains "/import")` |
| Counting dimension | IP address |
| Threshold | 2 requests per minute |
| Action | Block (HTTP 429) |
| Duration | 120 seconds |

Import endpoints process bulk payloads and are susceptible to resource exhaustion attacks. A strict threshold is appropriate.

### 3.6 Rate limit tuning guidance

After the first week of real traffic:

1. Open **Security** → **Analytics** → **WAF** in the Cloudflare dashboard.
2. Filter by action "Rate limit" to see triggered events.
3. If legitimate user IPs appear in the block log, increase the threshold for that rule by 50% and re-evaluate after another week.
4. If no events are seen on the token endpoint during normal operation, consider tightening to 3 req/10s.
5. Document each threshold change with a date and rationale in the operator's change log.

---

## 4. Cloudflare Access policies

Cloudflare Access (Zero Trust) provides identity-aware access control for operator-only surfaces. It requires a Cloudflare Zero Trust account, which is free for up to 50 users.

### 4.1 Grafana / observability (`ops.<domain>`)

Grafana must not be exposed publicly. If external operator access is needed, publish it through a Cloudflare Tunnel hostname behind an Access application.

**Steps to create the Access application:**

1. Go to **Zero Trust** → **Access** → **Applications** → **Add an application**.
2. Choose **Self-hosted**.
3. Set:

   | Field | Value |
   |---|---|
   | Application name | `FPS Observability` |
   | Session duration | `8 hours` |
   | Application domain | `ops.REPLACE_WITH_DOMAIN` |

4. Under **Policies** → **Add a policy**:

   | Field | Value |
   |---|---|
   | Policy name | `Operator allow-list` |
   | Action | Allow |
   | Rule type | Emails |
   | Emails | `REPLACE_WITH_OPERATOR_EMAIL_1`, `REPLACE_WITH_OPERATOR_EMAIL_2` |

   Or, if using SSO:

   | Rule type | Value |
   |---|---|
   | Identity provider group | `REPLACE_WITH_SSO_GROUP_NAME` |

5. Under **Authentication**, choose at minimum **One-time PIN** (email OTP). If the organisation has an identity provider (Google Workspace, Microsoft Entra ID, Okta), connect it under **Zero Trust** → **Settings** → **Authentication** and select it here.

6. Save the application.

7. In the Cloudflare Tunnel configuration, add a public hostname:

   | Public hostname | URL | Notes |
   |---|---|---|
   | `ops.REPLACE_WITH_DOMAIN` | `http://grafana:3000` | Access-protected; not publicly accessible without auth |

Users visiting `ops.<domain>` will be redirected to the Cloudflare Access login page before reaching Grafana.

### 4.2 Keycloak admin console

The Keycloak admin console is blocked at the WAF layer (section 1.2). For operator admin access, choose one of the following approaches:

**Option A — Local access only (recommended for NAS pilot):**
- Access the admin console via SSH tunnel or directly on the NAS LAN: `http://localhost:8080/admin`
- Do not create a public Cloudflare hostname for the admin console.
- This is the simplest and most secure option for a single-operator NAS deployment.

**Option B — Publish behind Cloudflare Access:**
- Create a separate Cloudflare Tunnel public hostname: `keycloak-admin.REPLACE_WITH_DOMAIN`
- Point it to `http://keycloak:8080`
- Create a Cloudflare Access application for `keycloak-admin.REPLACE_WITH_DOMAIN` with a strict allow-list (named operator emails only, not a domain-wide rule).
- The WAF block rule from section 1.2 should be scoped to `auth.<domain>` only, not to this hostname.
- **Do not use this option unless local access is impractical.** Exposing any admin console over the Internet increases the attack surface regardless of Access controls.

### 4.3 Access service tokens for automated systems

If CI/CD pipelines or monitoring systems need to call Access-protected endpoints:

1. Go to **Zero Trust** → **Access** → **Service Tokens** → **Create service token**.
2. Name the token `REPLACE_WITH_SERVICE_NAME`.
3. Copy the `CF-Access-Client-Id` and `CF-Access-Client-Secret` values. Store them in the operator's secret store — never in source control.
4. In the Access application policy, add a second policy rule with action Allow, rule type **Service Token**, and select the token.

---

## 5. NAS firewall origin rules

### 5.1 Firewall posture

The NAS firewall must allow no inbound connections from the Internet. All public traffic arrives via the outbound `cloudflared` tunnel. This section reinforces the port table from OPS011 and adds security rationale.

For the complete port block list, see [OPS011 — Step 1: Harden the NAS firewall](../production/nas-cloudflare-deployment-profile.md#step-1--harden-the-nas-firewall). That table is the authoritative reference; do not duplicate it here to avoid drift.

**Summary of required firewall state:**

| Direction | Rule |
|---|---|
| Inbound from `0.0.0.0/0` on any port except SSH from trusted IPs | **BLOCK** |
| Outbound to `0.0.0.0/0` on TCP 443 | **ALLOW** (required for `cloudflared` tunnel) |
| Outbound to `0.0.0.0/0` on TCP 80 | **ALLOW** (HTTP; block inbound) |
| SSH inbound from trusted operator IP range | **ALLOW** |
| All other inbound | **BLOCK** |

Apply these rules using the NAS firewall interface or Linux `iptables`/`ufw` before the tunnel is started.

### 5.2 Docker network isolation

All FairSpot services must remain on the Docker internal network. Verify that no service has published a port to `0.0.0.0` on the NAS host:

```bash
docker ps --format "table {{.Names}}\t{{.Ports}}" | grep "0.0.0.0"
```

Any line showing `0.0.0.0:<port>` for an internal service (MongoDB, RabbitMQ, Vault, MinIO, Dapr, FPS microservices) is a misconfiguration. Remove the `ports:` mapping from that service's `docker-compose.yaml` entry and use Docker internal networking only.

Only `cloudflared` itself needs outbound Internet access; it does not publish any ports.

### 5.3 Cloudflare IP allow-list (non-tunnel reference)

This section applies to operators who are **not** using Cloudflare Tunnel and instead expose a port directly on the origin server. It is included for completeness but does **not apply to the standard NAS Tunnel deployment** described in OPS011.

In a non-tunnel setup, you can restrict the origin HTTP/HTTPS port to accept connections only from Cloudflare's published IP ranges:

- IPv4 ranges: `https://www.cloudflare.com/ips-v4`
- IPv6 ranges: `https://www.cloudflare.com/ips-v6`

Example `ufw` rule to allow only Cloudflare IPs on port 443:

```bash
# Allow Cloudflare IPv4 ranges (example; retrieve current list from Cloudflare)
ufw allow from REPLACE_WITH_CLOUDFLARE_IPV4_CIDR to any port 443 proto tcp

# Block all other inbound on 443
ufw deny 443/tcp
```

> **Tunnel deployment:** When using `cloudflared` Tunnel, the NAS does not accept any inbound connections. The IP allow-list is irrelevant and should not be applied. The above example is for reference only.

---

## 6. TLS settings

Configure these settings in the Cloudflare dashboard under **SSL/TLS** for the zone.

### 6.1 SSL/TLS mode

| Setting | Required value |
|---|---|
| SSL/TLS encryption mode | **Full (strict)** |

Full (strict) mode requires a valid TLS certificate on the origin. In the NAS Tunnel deployment, `cloudflared` handles the connection from the Cloudflare edge to the NAS over an encrypted tunnel — no certificate is required on the NAS side for the Tunnel connection itself. However, set the mode to Full (strict) to ensure end-to-end certificate validation is enforced if the deployment topology changes.

> If the origin does not yet have a valid certificate and Full (strict) causes connection errors, use **Full** as a temporary fallback. Do not use **Flexible** — it transmits traffic in plaintext between Cloudflare and the origin.

### 6.2 Always Use HTTPS

**Dashboard path:** SSL/TLS → Edge Certificates → Always Use HTTPS → **On**

This setting redirects all HTTP requests to HTTPS at the Cloudflare edge before they reach the origin. Enable it for both `app.<domain>` and `auth.<domain>`.

### 6.3 HSTS

**Dashboard path:** SSL/TLS → Edge Certificates → HTTP Strict Transport Security (HSTS)

Recommended settings for a pilot going into production:

| Setting | Value |
|---|---|
| Enable HSTS | Yes |
| Max age | 6 months (15768000 seconds) — increase to 1 year after stable operation |
| Include subdomains | Yes (if all subdomains are HTTPS) |
| Preload | No (do not enable until the domain is confirmed stable on HTTPS for all subdomains) |
| No-Sniff header | Yes |

> **Warning:** HSTS is difficult to reverse once enabled with a long max-age. Start at 6 months and verify that all subdomains correctly serve HTTPS before enabling preload or extending the max-age.

### 6.4 Minimum TLS version

**Dashboard path:** SSL/TLS → Edge Certificates → Minimum TLS Version → **TLS 1.2**

TLS 1.0 and TLS 1.1 are deprecated. Set minimum version to TLS 1.2. If the user base does not include legacy clients, TLS 1.3 only is preferred.

### 6.5 Automatic HTTPS rewrites

**Dashboard path:** SSL/TLS → Edge Certificates → Automatic HTTPS Rewrites → **On**

This rewrites mixed-content HTTP links in pages to HTTPS at the edge. Enable it as a defence-in-depth measure.

---

## 7. Additional hardening settings

### 7.1 Bot Fight Mode

**Dashboard path:** Security → Bots → Bot Fight Mode → **On** (Free/Pro)

Enable Bot Fight Mode on all plans. On Pro and above, Super Bot Fight Mode provides more granular controls. The default setting — challenge or block known bots — is appropriate for the pilot.

### 7.2 Security level

**Dashboard path:** Security → Settings → Security Level → **Medium**

Medium challenges IPs with a moderate threat score. Review the threat analytics after go-live and adjust if legitimate users are being challenged.

### 7.3 Browser Integrity Check

**Dashboard path:** Security → Settings → Browser Integrity Check → **On**

Verifies that HTTP headers match a legitimate browser. Blocks many automated scrapers and scanners.

### 7.4 Challenge passage TTL

**Dashboard path:** Security → Settings → Challenge Passage → **30 minutes**

After a user passes a challenge, they are not re-challenged for this period. 30 minutes is a reasonable default for a booking application.

---

## 8. Acceptance checklist

The operator must tick off each item before allowing external customer traffic. This checklist maps to the security gate described in [OPS011 Before customer traffic](../production/nas-cloudflare-deployment-profile.md#before-customer-traffic).

### WAF rules

- [ ] Custom rule `FPS — Block internal paths on app` is active and set to Block.
- [ ] Verified by requesting `https://app.<domain>/metrics` from an external IP — response is HTTP 403.
- [ ] Verified by requesting `https://app.<domain>/dapr/v1.0/invoke/fps-booking/method/health` — response is HTTP 403.
- [ ] Verified by requesting `https://app.<domain>/swagger` — response is HTTP 403.
- [ ] Custom rule `FPS — Block Keycloak admin paths on auth` is active and set to Block.
- [ ] Verified by requesting `https://auth.<domain>/admin` — response is HTTP 403.
- [ ] Cloudflare Managed Ruleset is deployed and active, or Free-plan limitation is recorded as a gap.
- [ ] OWASP Core Ruleset is deployed, or Free-plan limitation is recorded as a gap with plan upgrade path.

### Rate limiting

- [ ] Rate limiting requires Cloudflare Pro — plan is confirmed as Pro or above, or Free-plan limitation is recorded as a gap.
- [ ] Login/token rate limit rule is active (5 req/10s per IP on token endpoints), or deferred due to Free plan.
- [ ] Booking submission rate limit rule is active (10 req/min per IP), or deferred due to Free plan.
- [ ] Draw trigger rate limit rule is active (2 req/min per IP), or deferred due to Free plan.
- [ ] HR cancellation rate limit rule is active (5 req/min per IP), or deferred due to Free plan.
- [ ] Import endpoint rate limit rule is active (2 req/min per IP), or deferred due to Free plan.

### Cloudflare Access

- [ ] `ops.<domain>` Access application is configured and tested — unauthenticated request to `https://ops.<domain>` redirects to Cloudflare Access login.
- [ ] Operator email OTP or SSO login to `ops.<domain>` has been verified by at least one operator.
- [ ] Keycloak admin console is either (a) not published as a public hostname, or (b) published only behind a named-operator Access policy and tested.

### NAS firewall

- [ ] `docker ps --format "table {{.Names}}\t{{.Ports}}" | grep "0.0.0.0"` returns no internal service ports.
- [ ] `curl -v https://<NAS-public-IP>:27017` returns connection refused or timeout.
- [ ] `curl -v https://<NAS-public-IP>:5672` returns connection refused or timeout.
- [ ] `curl -v https://<NAS-public-IP>:8200` returns connection refused or timeout.
- [ ] `curl -v https://<NAS-public-IP>:9090` returns connection refused or timeout.
- [ ] NAS host firewall rule blocking inbound on all ports except SSH from trusted IPs is confirmed.

### TLS

- [ ] SSL/TLS mode is set to Full (strict) or Full.
- [ ] Always Use HTTPS is enabled for the zone.
- [ ] HSTS is enabled with at least 6 months max-age.
- [ ] Minimum TLS version is set to TLS 1.2 or higher.
- [ ] Automatic HTTPS Rewrites is enabled.

### Bot and request protection

- [ ] Bot Fight Mode is enabled.
- [ ] Browser Integrity Check is enabled.
- [ ] Security level is set to Medium or higher.

---

## Document change log

| Date | Author | Change |
|---|---|---|
| 2026-05-29 | Claude | Initial WAF and origin hardening profile for issue #315 |
