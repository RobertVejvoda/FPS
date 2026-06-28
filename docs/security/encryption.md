# Encryption

FairSpot treats every non-local hosted profile as handling **Confidential** tenant and employee data by default. Local development may use plain HTTP and development credentials only for synthetic data on a developer machine. NAS, demo, pilot, and client-owned profiles must use encrypted communication and encrypted storage before they process real customer data.

## Profile Rules

| Profile | Encryption Rule |
| --- | --- |
| Local Docker | Plain HTTP and local development secrets are allowed only on the developer machine for synthetic data. |
| NAS hosted demo | Public access must use HTTPS through Cloudflare Tunnel/WAF. Internal container traffic remains private to the Docker network, with Dapr mTLS and storage encryption tracked as production-readiness gaps until proven. |
| Client-owned production | Public, service-to-service, storage, backup, and secret-management encryption must be implemented and evidenced in the deployment profile before real employee data is processed. |

## Data In Transit

Public browser, mobile, and API communication must use HTTPS outside the local profile. For Release 1 this means:

- `https://app.fairspot.net` for the web application and public API gateway;
- `https://auth.fairspot.net` for Keycloak/OIDC;
- Cloudflare Tunnel between Cloudflare and the NAS so no inbound NAS port is opened to the Internet;
- WAF rules that expose only intended app/auth paths and block internal services, Dapr APIs, observability endpoints, and storage/admin ports.

Internal service-to-service communication should use Dapr mTLS in hosted profiles. The local profile keeps Dapr mTLS disabled for developer simplicity. Hosted Dapr mTLS enablement and evidence are tracked as a production-readiness gap.

## Data At Rest

Hosted profiles must encrypt authoritative stores, derived stores, object storage, and backups. This includes:

- MongoDB/Dapr state data;
- RabbitMQ durable queues where used;
- MinIO or object storage buckets;
- Keycloak database/state;
- Grafana/Loki/Prometheus/Jaeger data when retained;
- NAS volumes and backups.

FairSpot cannot prove NAS disk encryption from application code alone. The deployment runbook must record whether the Synology volume/shared folder and backup target are encrypted, who owns the recovery keys, and how restore evidence is captured. Operators complete the [NAS encryption-at-rest and backup evidence checklist](../production/nas-encryption-backup-evidence.md) (OPS019) before any real customer data is processed.

## Secrets And Keys

Secrets are classified as **Secret** data. They must not be committed, logged, pasted into PRs, or stored in reusable demo scripts. Hosted profiles use:

- `code/infrastructure/nas.env` for local operator-controlled stack secrets; this file is gitignored;
- `code/infrastructure/cloudflared/.env.nas` for the Cloudflare Tunnel token; this file is gitignored;
- Dapr secret-store components so application services read runtime secrets through Dapr rather than hardcoded values.

The current NAS profile still uses Vault development mode as a transitional implementation. A production-grade secret store, rotation process, and recovery procedure are required before processing real customer data.

## Evidence Required Before Customer Data

Before FairSpot processes real customer data in any non-local profile, the deployment evidence must show:

- public app/auth URLs are HTTPS only;
- Cloudflare/WAF blocks internal and diagnostic endpoints;
- no application, database, broker, Dapr, or observability admin port is publicly reachable;
- Dapr mTLS or an approved equivalent protects service-to-service traffic;
- data stores and backup targets are encrypted at rest;
- secrets are stored outside Git and rotated when exposed;
- smoke tests record the exact deployed domain, realm, and relevant security checks.

## Open Gaps

See [Security Gap Register](./gap-register.md) for the current production-blocking gaps around Dapr mTLS, secret management, storage encryption, backup evidence, WAF evidence, and hosted demo smoke gates.
