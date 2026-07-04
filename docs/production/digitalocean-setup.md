# DigitalOcean Setup

DigitalOcean is the FairSpot-operated cloud-hosted follow-up target after the Release 1 NAS/Cloudflare hosted evaluation path. This page records the target shape only; it is not a secrets, pricing, or step-by-step operator runbook.

## Target Shape

| Area | Direction |
| --- | --- |
| Container runtime | Start with a DigitalOcean Droplet running the containerized stack through Docker Compose. Move to DOKS only if Kubernetes evidence or cluster-level operation is required. |
| Container registry | GHCR remains acceptable. DigitalOcean Container Registry is optional if it simplifies image pull credentials or deployment automation. |
| Ingress | Cloudflare in front where approved, or DigitalOcean Load Balancer when the profile explicitly needs it. Public endpoints must use HTTPS. |
| Identity | Keycloak first, with client/managed OIDC only when a pilot requires it. |
| State stores | Self-hosted stores initially. Evaluate DigitalOcean Managed Databases for PostgreSQL, MongoDB-compatible alternatives where approved, Valkey/Redis, OpenSearch, or other state services only when they improve durability or operational evidence. |
| Pub/sub | RabbitMQ first, or another Dapr-compatible broker later if evidence requires it. |
| Object storage | MinIO initially; DigitalOcean Spaces when hosted object storage is needed for reports, exports, backups, or attachments. |
| Secrets | Vault or profile-approved secret injection through the Dapr secret-store boundary. No secrets in manifests, screenshots, issues, or docs. |
| Observability | Grafana/Prometheus/Loki/Jaeger first, plus OpenTelemetry export. DigitalOcean Monitoring can provide host/resource visibility but does not replace application telemetry or Audit service records. |
| Backup/restore | Droplet snapshots and service-specific backups first. Use managed database backups if state moves to managed services. Restore evidence is required before customer data. |

## Implementation Order

1. Prove the Release 1 NAS/Cloudflare hosted path and public smoke checklist.
2. Create the DigitalOcean Droplet/Docker Compose profile using the same logical Dapr component names as local/NAS.
3. Add deployment, seed/reset, backup/restore, and teardown evidence for synthetic demo data.
4. Evaluate managed databases, Spaces, Container Registry, Load Balancer, or DOKS only when a concrete evidence need exists.

## Non-Goals

- Do not reintroduce AWS or Azure as FairSpot-operated target-cloud plans.
- Do not publish static provider prices; validate current prices before sharing externally.
- Do not store secrets, tokens, private keys, or connection strings in documentation.
- Do not require application service code changes for provider movement; use deployment profiles, Dapr components, and OpenTelemetry configuration.

## Official Provider References

- [DigitalOcean App Platform](https://docs.digitalocean.com/products/app-platform/)
- [DigitalOcean Kubernetes](https://docs.digitalocean.com/products/kubernetes/)
- [DigitalOcean Managed Databases](https://docs.digitalocean.com/products/databases/)
- [DigitalOcean Spaces](https://docs.digitalocean.com/products/spaces/)
