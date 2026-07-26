# AWS Setup

This page is a legacy compatibility stub. AWS is not an active FairSpot-operated target cloud.

The active hosting direction is:

- Primary FairSpot-operated hosted evaluation: DOKS/Cloudflare in [Hosting and Deployment Strategy](./hosting-deployment-strategy).
- Secondary self-hosted/fallback evidence: [NAS/Cloudflare deployment profile](./nas-cloudflare-deployment-profile) and [DigitalOcean Droplet Setup](./digitalocean-setup).
- Client-owned production: provider-neutral contracts in [Hosting and Deployment Strategy](./hosting-deployment-strategy) and [Deployment Profiles](../architecture/technology/deployment-profiles).

Do not use the old AWS service list or static cost estimates for planning. Client AWS deployments are possible only when a client explicitly selects AWS and provides tested Dapr component manifests, secrets, storage, monitoring, backup/restore, and runbook evidence for that client-owned environment.
