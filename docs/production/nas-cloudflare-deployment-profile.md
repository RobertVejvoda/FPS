# NAS Cloudflare Deployment Profile

> **Moved private (#684):** the detailed hosted-operator runbook now lives in the private `fairspot-platform` repository at `docs/runbooks/nas-cloudflare-deployment-profile.md`.

This public page is intentionally a summary only. The NAS + Cloudflare Tunnel profile is an operator deployment path for the FairSpot hosted pilot. It includes environment-specific routing, tunnel, gateway, firewall, recovery, and smoke-test steps that belong in the private platform plane, not in the open-core product documentation.

## Public Contract

Any hosted or client-owned FairSpot deployment must satisfy these public requirements:

| Area | Requirement |
| --- | --- |
| Ingress | Public traffic enters through HTTPS only. Direct service/container ports must not be exposed to the Internet. |
| Identity | Tenant and user identity come from the configured OIDC provider and signed tokens, never from caller-supplied request bodies. |
| Runtime | FairSpot services run as containers with Dapr sidecars or equivalent Dapr-compatible runtime components. |
| Service-to-service security | Dapr mTLS is the target for the FairSpot-operated Kubernetes/DOKS profile. On the Release 1 NAS/self-hosted Docker Compose profile it is a documented exception (mTLS disabled — no Sentry control plane; single-host private bridge). Startup reports the active Dapr security mode. See [Dapr-First Production Standards](./dapr-first-production-standards) (OPS017). |
| Secrets | Tunnel tokens, certificates, passwords, keys, and connection strings are injected from a secret-management system and are never committed. |
| Storage | State stores, read models, and backup targets are tenant-safe and encrypted according to the selected deployment profile. |
| Operations | Deployment must have backup/restore evidence, incident handling, maintenance expectations, and public-boundary smoke evidence before real customer data is processed. |

## Public References

- [Production](../production): public deployment and operations overview.
- [Deployment Profiles](../architecture/technology/deployment-profiles): target architecture for deployment options.
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary): public/private documentation split.
- [Dapr-First Production Standards](./dapr-first-production-standards): runtime portability and Dapr hardening expectations.
