# Customer-First Deployment Gap Analysis

This page reconciles the current FairSpot documentation and implementation state against the next practical goal: make FairSpot usable by first customers on a real domain while the runtime is still hosted locally on the NAS.

The target is not full client-owned enterprise production yet. The target is a customer-facing pilot environment with synthetic or approved pilot data, a stable HTTPS domain, Cloudflare in front of the NAS, clear operator runbooks, and enough evidence that the product works for employee, HR, and administrator users.

## Target Deployment

| Area | Customer-first target |
| --- | --- |
| Hosting | NAS-hosted FairSpot stack behind Cloudflare Tunnel or another origin path that does not expose raw NAS service ports. |
| Domain | Public HTTPS hostnames such as `app.<domain>` for FairSpot and `auth.<domain>` for the IdP. |
| Edge protection | Cloudflare DNS proxy, TLS, WAF custom rules, managed rules where plan permits, rate limiting where plan permits, DDoS protection, and Cloudflare Access for operator-only surfaces. |
| Origin hardening | No direct public access to internal services, Dapr sidecars, metrics, Swagger/OpenAPI, database, broker, MinIO, or Keycloak admin. |
| Identity | Real OIDC URLs and redirect URIs for the public domain; tenant/user/role claims remain the source of truth. |
| Persistence | No customer data in evaluation-grade in-memory stores. Booking, Profile, Configuration, Notification, Audit, and Reporting must use tenant-scoped persistent storage or an explicitly approved pilot limitation. |
| Evidence | A repeatable smoke test proves login, booking request, Draw, notifications, audit, reporting, HR/admin operations, backup/restore, and log review. |
| Mobile | Use hosted API/OIDC config and internal distribution first. App Store / Google Play submission comes after the hosted web/API path is stable. |

## What Is Implemented

| Capability | Current implementation evidence | Customer-readiness assessment |
| --- | --- | --- |
| Local full-stack harness | `docs/production/local-test-harness.md`, `tools/start-local-harness.sh`, seeded users, gateway on `localhost:10000`. | Good developer baseline; not yet a public domain deployment profile. |
| Demo seed and Draw evidence | `tools/dev-seed.sh` seeds Green Logistics (`GL-HQ`) demo data, triggers a Draw, and asserts the outcome (`verify_demo_draw`). | Draw reads the seeded Configuration slots over Dapr and produces visible allocations / company-car Tier-1 pre-allocation (#666); still needs hosted smoke evidence and reset runbook. |
| Employee booking and My Spots | Booking APIs/mobile flows implemented; UX work is tracked by `UX007` / issue #303. | Mostly usable; customer pilot depends on final mobile/web polish and hosted validation. |
| Default vehicle | PR #312 implemented `IsDefault` and default mobile preselection. | Merged; ready for the next seed/demo pass. |
| HR operations design | Issue #310 documents HR needs: operational workspace, Draw visibility/run action, cancellation with notification. | Not complete until implementation PR is validated and merged. |
| Administrator workspace design | Issue #311 documents tenant readiness/admin default view. | Not complete until implementation PR is validated and merged. |
| Local observability | Local OpenTelemetry, metrics, Grafana/Loki/Jaeger evidence are documented. | Needs NAS-hosted log/metric retention and operator access controls. |
| Production handoff model | `docs/production/client-production-handoff.md` defines BYOC responsibilities. | Good enterprise direction; too broad for the immediate NAS/Cloudflare pilot. |

## Blocking Gaps

| Priority | Gap | Why it blocks customer-first deployment | Source |
| --- | --- | --- | --- |
| P0 | NAS + Cloudflare deployment profile is missing. | There is no repeatable public-domain setup for the current NAS-hosted path. | `docs/production/hosting-deployment-strategy.md`, Cloudflare Tunnel docs. |
| P0 | WAF/rate-limit/origin-hardening policy is missing. | Public domain exposure without edge controls leaves login, API, and internal paths too easy to probe. | `docs/security/gap-register.md`, Cloudflare WAF docs. |
| P0 | Public-domain auth and gateway configuration are missing. | Keycloak/OIDC issuer, redirect URIs, CORS, secure cookies, and Envoy routes must use the real domain, not localhost assumptions. | `docs/production/client-production-handoff.md`. |
| P0 | Persistent tenant-scoped stores are incomplete. | Tenant storage contract marks Booking key gaps and in-memory repositories as production-blocking. | `docs/production/tenant-storage-contract.md`. |
| P0 | Hosted smoke/readiness evidence is missing. | We need proof that the real public URL works end-to-end after deployment and after reset. | `docs/production/demo-environment-baseline.md`, `docs/production/tenant-onboarding-smoke.md`. |
| P1 | HR and Administrator role views are not merged. | First customers need clear role-specific defaults, Draw operation visibility, and support cancellation workflow. | Issues #310 and #311. |
| P1 | Tenant onboarding remains partly evaluation-grade. | IdP mapping, first-admin path, tenant object storage, branding, and admin setup are not complete. | `docs/production/tenant-onboarding-smoke.md`. |
| P1 | Retention jobs and privacy durable-store evidence remain incomplete. | Customer pilot with personal data needs retention and GDPR evidence or explicit approval to limit data scope. | `docs/security/gap-register.md`, `docs/security/security-review-pack.md`. |
| P2 | Store-ready mobile release process is not prepared. | App Store / Google Play require signing, account verification, metadata, privacy/data-safety disclosures, and review evidence. | Apple Developer and Google Play Console docs. |

## Cloudflare Setup Direction

Cloudflare Tunnel is the preferred first NAS path because the NAS does not need inbound ports opened directly. Cloudflare documents that a tunnel public hostname maps a public hostname to a local service and applies Cloudflare CDN, WAF, and DDoS protections before traffic reaches the origin. The setup docs also support running `cloudflared` as a Docker container with a tunnel token.

Recommended public hostnames:

| Hostname | Origin target | Exposure |
| --- | --- | --- |
| `app.<domain>` | NAS reverse proxy or Envoy web/API gateway | Public to authenticated users. |
| `auth.<domain>` | Keycloak public login endpoints | Public login only; admin console protected or not published. |
| `ops.<domain>` | Grafana/observability, if needed | Protected by Cloudflare Access; not public. |

Initial Cloudflare controls:

- DNS records proxied through Cloudflare.
- Cloudflare Tunnel from NAS to Cloudflare for public hostnames.
- TLS Full (strict) where an origin certificate is used; Tunnel can avoid public origin certificate exposure for HTTP services behind the connector.
- WAF custom rules to block direct access to `/metrics`, Dapr paths, internal service paths, Keycloak admin, debug routes, and API documentation unless explicitly allowed for operators.
- Managed rules and OWASP rules where the selected Cloudflare plan permits them.
- Rate limiting for login/token endpoints, booking submission, Draw trigger, imports, and cancellation endpoints. Cloudflare documents rate limiting rules for abuse protection such as login brute-force defense; exact rule counts and periods depend on plan.
- Cloudflare Access for operator-only surfaces, staging/admin utilities, and observability.
- NAS firewall rules that do not expose internal app, database, broker, MinIO, Dapr, or metrics ports publicly.

Cloudflare official references used for this direction:

- Cloudflare Tunnel setup: https://developers.cloudflare.com/tunnel/setup/
- Cloudflare Tunnel routing/public hostnames: https://developers.cloudflare.com/tunnel/routing/
- Cloudflare WAF concepts: https://developers.cloudflare.com/waf/concepts/
- Cloudflare rate limiting rules: https://developers.cloudflare.com/waf/rate-limiting-rules/

## Mobile Store Direction

For first customer deployment, do not make App Store / Google Play publication the critical path. Use the hosted domain with:

- responsive web for customer validation where possible;
- Expo/internal native builds for mobile validation;
- TestFlight for iOS beta once the Apple Developer Program account exists;
- Google Play internal or closed testing once the Play Console account exists.

Store submission becomes a later P2 release gate after backend domain, auth, privacy, and support flows are stable.

Current official constraints:

- Apple Developer Program membership is listed by Apple as USD 99 per year and includes TestFlight and App Store distribution. TestFlight supports inviting up to 10,000 external testers.
- Google Play Console setup includes a one-time USD 25 registration fee, developer identity verification, account type selection, and, for some personal-account paths, testing requirements before production availability.

Official references:

- Apple Developer Program: https://developer.apple.com/programs/
- Apple Developer Program enrollment: https://developer.apple.com/programs/enroll/
- Google Play Console account setup: https://support.google.com/googleplay/android-developer/answer/6112435
- Google Play account types: https://support.google.com/googleplay/android-developer/answer/13634885
- Google Play required account information: https://support.google.com/googleplay/android-developer/answer/13628312

## Prioritized Claude Slices

These are ordered for customer-first deployability, not long-term enterprise completeness.

| Priority | Slice | Implementer | Expected result |
| --- | --- | --- | --- |
| P0 | `OPS011` NAS Cloudflare deployment profile (#313) | Claude | Repeatable NAS deployment profile, Cloudflare Tunnel config template, domain runbook, no committed secrets. |
| P0 | `SEC010` Cloudflare WAF and origin hardening (#315) | Claude | WAF/rate-limit/access/origin rules documented and represented as deployable templates/checklists. |
| P0 | `OPS012` Public-domain auth and gateway profile (#316) | Claude | Keycloak/OIDC, Envoy, web, and mobile config for `app.<domain>` / `auth.<domain>` with CORS and redirect URIs. |
| P0 | `DATA010` Persistent tenant-scoped storage readiness (#317) | Claude | Production-blocking tenant key and in-memory-store gaps split into executable implementation steps with smoke evidence. |
| P0 | `OPS013` Hosted customer smoke and reset evidence (#314) | Claude | Script/runbook validates login, booking, Draw, notification, audit, reporting, HR/admin views, reset, and logs through the public domain. |
| P1 | #310 HR operations workspace | Claude | HR default view, Draw visibility/action, request cancellation with notification. |
| P1 | #311 Administrator default workspace | Claude | Admin landing surface for tenant readiness, configuration, identity/onboarding evidence, and smoke status. |
| P1 | `CUST011` Tenant onboarding hardening for first customer (#319) | Claude | IdP mapping, first admin, object storage readiness, branding, and admin setup gaps resolved or explicitly deferred. |
| P2 | `MOB010` Mobile hosted build and store-readiness plan (#318) | Claude | EAS/internal distribution config, TestFlight/Play internal testing plan, store metadata/privacy checklist. |

## Acceptance Gate

Before the first customer sees the new domain:

1. `app.<domain>` and `auth.<domain>` are reachable over HTTPS through Cloudflare.
2. Origin services are not directly reachable from the Internet.
3. Login, booking creation, booking list, Draw run/status, notification, audit, reporting, HR, and admin smoke pass on the public domain.
4. Tenant/customer data stores are persistent and tenant-scoped, or customer pilot scope explicitly says which non-persistent stores are allowed.
5. Backup/restore and reset runbooks have been tested once.
6. WAF, rate limiting, and Access policies are documented with screenshots or exported rule definitions.
7. Mobile can point at the hosted domain using internal distribution or web fallback.
