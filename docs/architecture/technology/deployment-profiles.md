# Deployment Profiles

| Profile | Purpose | Key Components | Responsibilities | Status |
| --- | --- | --- | --- | --- |
| Local development | Developer and agent validation. | Local harness, Docker Compose/local containers, self-hosted Dapr components, Keycloak, service ports, web/mobile smoke paths, local observability. | Developer/operator starts and validates locally. | Partial |
| NAS/Cloudflare hosted pilot | First public-domain customer evaluation path hosted locally on NAS. | Cloudflare Tunnel, Cloudflare WAF/rate limiting/Access, Envoy/API gateway, Keycloak public login, internal-only services/Dapr/databases/broker/observability, backup/restore, smoke runbooks. | Robert/Codex operate and validate readiness/risk using production runbooks as evidence. | Partial |
| DigitalOcean hosted demo | Optional cloud-hosted demo/evaluation path after the Release 1 NAS/Cloudflare profile. | DigitalOcean Droplet with Docker Compose first; DOKS later only if Kubernetes evidence is required; Dapr-compatible broker/state/secrets, OIDC, OpenTelemetry export, reset/teardown, demo seed. | FairSpot controls demo cost and evidence. | Placeholder |
| Client-owned production | Client-operated deployment. | Client identity, secrets, storage, telemetry, backup, ingress, support boundary, Dapr/OpenTelemetry component contracts. | Client IT owns production operation; FairSpot provides guidance and artifacts. | Placeholder |
| Enterprise Kubernetes | Client-required enterprise platform option. | Kubernetes, Dapr runtime/extension, private networking, workload identity, client observability/security stack. | Client platform team owns operation. | Deferred |

## Profile Rules

- Application services should not change code when moving between local, NAS, demo, and client production profiles.
- Dapr component bindings and OpenTelemetry exporters are the portability boundary.
- Provider-specific scripts are allowed only in deployment/profile folders, not inside business logic.
- Demo and production profiles must use real secret stores, not committed files.
- Local profiles may use documented default credentials because they are disposable developer environments. Hosted profiles must refuse to start when required operator secrets are missing.
- Public profile ingress must expose only intended web/API/auth endpoints.
- Local-only profiles may use HTTP on loopback/LAN for development. Every non-local profile must use encrypted external communication (`https://` public endpoints) and must not expose raw internal HTTP service ports.
- Internal service ports, databases, brokers, Dapr sidecars, metrics, Swagger/OpenAPI, Keycloak admin, and observability backends must not be public.
- Mobile store release is not the first deployment gate; internal/TestFlight/Play internal testing follows after hosted web/API/auth are stable.
- Operational procedures stay in `production/` runbooks; this page states profile boundaries and acceptance gates.

## Operator Experience Target

The target deployment experience is one command after one-time environment setup.

One-time setup is still required because the operator must create secrets and domain resources that cannot safely be committed or generated blindly:

1. Create or copy the NAS environment file from `code/infrastructure/nas.env.example`.
2. Fill all required secrets with unique values from a password manager.
3. Create the Cloudflare Tunnel and copy its token into the ignored tunnel env file.
4. Configure public hostnames in Cloudflare.
5. Configure the hosted Keycloak realm and clients for the public URLs.

After that, the expected NAS operation path is:

```bash
./tools/deploy-nas.sh --domain fairspot.net
```

The wrapper starts the full container stack, starts `cloudflared`, and runs public-domain checks. The lower-level `./tools/start-container-stack.sh --nas --env-file code/infrastructure/nas.env` remains the reusable health gate for CI/manual troubleshooting.

This is intentionally "single command", not "zero configuration": credentials, DNS ownership, WAF policy, and identity-provider settings are security boundaries and must remain explicit.

## NAS/Cloudflare Target

The immediate customer-first target is NAS-hosted FairSpot behind Cloudflare Tunnel:

- `app.fairspot.net` routes through Cloudflare to the API/web gateway for Release 1.
- `auth.fairspot.net` routes through Cloudflare to public Keycloak login endpoints for Release 1.
- Public app/auth URLs must be HTTPS. Cloudflare terminates public TLS and routes to private Docker-network HTTP origins through the encrypted tunnel.
- Release 1 uses one Keycloak realm named `fairspot` for demo and Green Logistics users.
- Tenant separation is enforced by application tenant claims and authorization, not by separate realms.
- Separate realms are deferred until a real customer requires identity administration isolation.
- NAS operation requires Docker/Container Manager only. .NET and Dapr are runtime containers, not host-installed prerequisites.
- Keycloak admin, Grafana/Prometheus/Jaeger/Loki, databases, brokers, MinIO, Vault, services, and Dapr sidecars remain private.
- Operator-only surfaces use local access or Cloudflare Access, not public exposure.
- The NAS overlay enforces real credentials for Keycloak, Grafana, MongoDB, RabbitMQ, MinIO, Vault, and FairSpot token validation settings before startup.
- WAF custom rules block internal/debug paths and rate-limit abuse-sensitive endpoints.
- Smoke evidence must cover login, booking, Draw, notifications, audit, reporting/read-models, HR/admin operations, reset, backup/restore, and log review.
- Hosted smoke evidence must be recorded before customer data is allowed. Localhost smoke can prove script behavior, but public-domain checks remain pending until run against the real domain.
- Backup/restore evidence must include at least one restore drill for authoritative state or an explicit accepted pilot waiver.

## Customer-Owned Production Target

Client production is bring-your-own-cloud/platform. FairSpot should provide:

- container images or build instructions;
- Dapr component contracts for pub/sub, state, workflow, bindings, secrets, and service invocation;
- OpenTelemetry instrumentation and exporter guidance;
- identity claim mapping requirements;
- tenant storage provisioning and index guidance;
- backup, restore, incident, retention, and access-control runbooks;
- RTO/RPO targets and restore evidence expectations;
- sizing assumptions and evidence from demo/staging.

## Visible Deployment Gaps

- Hosted smoke/readiness evidence is not complete.
- Persistent tenant-scoped stores are not complete for all P0 state.
- WAF/rate-limit/origin-hardening policy needs executable profile evidence.
- Backup/restore and reset runbooks need hosted validation.
- RTO/RPO targets need review before paid production commitments.
- Client-owned production handoff should stay guidance until the NAS/customer-first profile is proven.

## Source Evidence

- [Production](/production)
- [Hosting Strategy](/production/hosting-deployment-strategy)
- [Customer-First Deployment Gap Analysis](/production/customer-first-deployment-gap-analysis)
- [NAS Cloudflare Deployment Profile](/production/nas-cloudflare-deployment-profile)
