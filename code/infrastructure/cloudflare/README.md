# Cloudflare edge hardening — codified rules (SEC010 / #764)

Versioned, repeatable definitions of the FairSpot Cloudflare edge hardening. These files are the machine-consumable form of the prose in
[`docs/security/cloudflare-waf-profile.md`](../../../docs/security/cloudflare-waf-profile.md) (the authoritative source for rationale, ordering, plan dependencies, and TLS/Access settings).

| File | Cloudflare phase | What it does |
| --- | --- | --- |
| `waf-custom-rules.json` | `http_request_firewall_custom` | Blocks (403) internal/diagnostic paths on `app.<domain>` (`/metrics`, `/dapr/`, `/v1.0/`, `/healthz`, `/swagger`, `/openapi`, `/admin`, `/_`, each also in `/api/`-prefixed form) and the Keycloak admin surfaces on `auth.<domain>`. |
| `rate-limit-rules.json` | `http_ratelimit` | Rate-limits (429) login/token, booking submission, Draw trigger, HR cancellation, and import endpoints. **Counting rate limits require Cloudflare Pro or above.** |

## No secrets here

These files carry only **path expressions, thresholds, and actions** — safe to keep in the open-core repo. Everything account-specific stays out:

- the domain is a `REPLACE_WITH_DOMAIN` placeholder;
- the Cloudflare **zone ID** and **API token** are supplied at apply time from the operator secret store;
- the real domain, the apply invocation with live secrets, and captured evidence live in the private `fairspot-platform` operator runbook (per the [Open-Core Boundary](../../../docs/strategy-layer/open-core-boundary.md)).

`git grep REPLACE_WITH_DOMAIN code/infrastructure/cloudflare` should be the only domain reference; there must be no zone IDs or tokens in these files.

## Applying to a zone

Each file targets a **phase entrypoint ruleset**. Substitute the domain and `PUT` the `rules` array to the Cloudflare Rulesets API. Provide `CF_API_TOKEN` (a token scoped to *Zone → Zone WAF → Edit*) and `CF_ZONE_ID` from your secret store — never inline them:

```bash
# From the operator secret store — do NOT commit these:
: "${CF_API_TOKEN:?set from secret store}"
: "${CF_ZONE_ID:?set from secret store}"
: "${FPS_DOMAIN:?e.g. example.com}"

cd code/infrastructure/cloudflare
for f in waf-custom-rules.json rate-limit-rules.json; do
  phase=$(jq -r .phase "$f")
  jq --arg d "$FPS_DOMAIN" \
     '{rules: [.rules[] | del(._comment) | .expression |= gsub("REPLACE_WITH_DOMAIN"; $d)]}' "$f" \
  | curl -fsS -X PUT \
      "https://api.cloudflare.com/client/v4/zones/$CF_ZONE_ID/rulesets/phases/$phase/entrypoint" \
      -H "Authorization: Bearer $CF_API_TOKEN" \
      -H "Content-Type: application/json" \
      --data @- >/dev/null && echo "applied: $f"
done
```

Rule ordering follows the file order (internal-path block first, then Keycloak admin; rate limits after). Managed rulesets / OWASP CRS and Cloudflare Access are **plan-dependent** and stay dashboard/operator steps — see the profile doc §2 and §4.

## Verifying

The hosted smoke checks that the rules are actually live. Run it against the public domain:

```bash
./tools/start-container-stack.sh --nas --smoke-only \
  --app-host <app-host> --auth-host <auth-host>
# or the full evidence writer:
APP_URL=https://app.<domain>/api AUTH_URL=https://auth.<domain> OIDC_REALM=fairspot ./tools/smoke-hosted.sh
```

It fails if `/metrics`, Dapr paths, internal service hostnames, Keycloak admin, API docs, or app-root debug routes are publicly reachable, and (against a real Cloudflare domain) that a burst to the token endpoint is rate-limited (`429`). Off a real Cloudflare domain, the rate-limit and WAF checks report PENDING rather than passing.
