# Deployment Profiles

| Profile | Purpose | Key Components | Responsibilities |
| --- | --- | --- | --- |
| Local development | Developer and agent validation. | Local harness, Dapr components, Keycloak, service ports, web/mobile smoke paths. | Developer/operator starts and validates locally. |
| Hosted pilot | Public-domain demo/evaluation path. | NAS or low-cost host, Cloudflare/WAF, OIDC, Dapr runtime, backup/restore, smoke runbooks. | Robert/Codex validate readiness and risk. |
| Customer-owned production | Client-operated deployment. | Client identity, secrets, storage, telemetry, backup, ingress, support boundary. | Client IT owns production operation; FairSpot provides guidance. |

## Source Evidence

- [Production](/production)
- [Hosting Strategy](/production/hosting-deployment-strategy)
- [Customer-First Deployment Gap Analysis](/production/customer-first-deployment-gap-analysis)
