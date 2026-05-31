# Deployment Profiles

| Profile | Purpose | Key Components | Responsibilities | Status |
| --- | --- | --- | --- | --- |
| Local development | Developer and agent validation. | Local harness, Docker Compose/local containers, self-hosted Dapr components, Keycloak, service ports, web/mobile smoke paths, local observability. | Developer/operator starts and validates locally. | Partial |
| NAS/Cloudflare hosted pilot | First public-domain customer evaluation path hosted locally on NAS. | Cloudflare Tunnel, Cloudflare WAF/rate limiting/Access, Envoy/API gateway, Keycloak public login, internal-only services/Dapr/databases/broker/observability, backup/restore, smoke runbooks. | Robert/Codex operate and validate readiness/risk. | Partial |
| Low-cost hosted demo | Optional cloud-hosted demo/evaluation path. | Managed container runtime or small Kubernetes, Dapr-compatible broker/state/secrets, OIDC, OpenTelemetry export, reset/teardown, demo seed. | FairSpot controls demo cost and evidence. | Placeholder |
| Client-owned production | Client-operated deployment. | Client identity, secrets, storage, telemetry, backup, ingress, support boundary, Dapr/OpenTelemetry component contracts. | Client IT owns production operation; FairSpot provides guidance and artifacts. | Placeholder |
| Enterprise Kubernetes | Client-required enterprise platform option. | Kubernetes, Dapr runtime/extension, private networking, workload identity, client observability/security stack. | Client platform team owns operation. | Deferred |

## Profile Rules

- Application services should not change code when moving between local, NAS, demo, and client production profiles.
- Dapr component bindings and OpenTelemetry exporters are the portability boundary.
- Provider-specific scripts are allowed only in deployment/profile folders, not inside business logic.
- Demo and production profiles must use real secret stores, not committed files.
- Public profile ingress must expose only intended web/API/auth endpoints.
- Internal service ports, databases, brokers, Dapr sidecars, metrics, Swagger/OpenAPI, Keycloak admin, and observability backends must not be public.
- Mobile store release is not the first deployment gate; internal/TestFlight/Play internal testing follows after hosted web/API/auth are stable.

## NAS/Cloudflare Target

The immediate customer-first target is NAS-hosted FairSpot behind Cloudflare Tunnel:

- `app.<domain>` routes through Cloudflare to the API/web gateway.
- `auth.<domain>` routes through Cloudflare to public Keycloak login endpoints.
- Keycloak admin, Grafana/Prometheus/Jaeger/Loki, databases, brokers, MinIO, Vault, services, and Dapr sidecars remain private.
- Operator-only surfaces use local access or Cloudflare Access, not public exposure.
- WAF custom rules block internal/debug paths and rate-limit abuse-sensitive endpoints.
- Smoke evidence must cover login, booking, Draw, notifications, audit, reporting/read-models, HR/admin operations, reset, backup/restore, and log review.

## Customer-Owned Production Target

Client production is bring-your-own-cloud/platform. FairSpot should provide:

- container images or build instructions;
- Dapr component contracts for pub/sub, state, workflow, bindings, secrets, and service invocation;
- OpenTelemetry instrumentation and exporter guidance;
- identity claim mapping requirements;
- tenant storage provisioning and index guidance;
- backup, restore, incident, retention, and access-control runbooks;
- sizing assumptions and evidence from demo/staging.

## Visible Deployment Gaps

- Hosted smoke/readiness evidence is not complete.
- Persistent tenant-scoped stores are not complete for all P0 state.
- WAF/rate-limit/origin-hardening policy needs executable profile evidence.
- Backup/restore and reset runbooks need hosted validation.
- Client-owned production handoff should stay guidance until the NAS/customer-first profile is proven.

## Source Evidence

- [Production](/production)
- [Hosting Strategy](/production/hosting-deployment-strategy)
- [Customer-First Deployment Gap Analysis](/production/customer-first-deployment-gap-analysis)
- [NAS Cloudflare Deployment Profile](/production/nas-cloudflare-deployment-profile)
