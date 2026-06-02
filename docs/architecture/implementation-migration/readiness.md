# Readiness

Readiness records the evidence required before FairSpot can be treated as customer-ready for hosted pilot or later production handoff.

## Readiness Checklist

| Area | Evidence Required | Status | Notes |
| --- | --- | --- | --- |
| Security | Authenticated tenant/user context, tenant isolation tests, WAF/ingress controls, secrets handling, privacy controls, and audit evidence. | Partial | Source: [Security Architecture](/architecture/security/), [Security Gap Register](/architecture/security/gap-register), [NAS Cloudflare Deployment](/production/nas-cloudflare-deployment-profile). |
| Operational | Health endpoints, logs, metrics, traces, backup/restore, smoke runbooks, maintenance expectations, and support boundaries. | Partial | Source: [Hosted Smoke Runbook](/production/hosted-smoke-runbook), [Backup And Restore](/production/backup-restore), [Monitoring](/production/monitoring). |
| Data | Service-owned durable stores, tenant-scoped keys, projection rebuild strategy, event inbox, and recovery evidence. | Partial | Customer persistence evidence exists; DataHub projections and several service stores remain gaps. |
| Hosted Profile | NAS/Cloudflare/WAF public profile, private service exposure, auth callback, reset/reseed, no internal exposure, and log review. | Open | Must be validated before real customer data. |
| Mobile | Expo build, environment configuration, real device testing, auth/session behavior, and pilot distribution path. | Open | Source: [Mobile Device Testing](/production/mobile-device-testing). |

## Readiness Outcomes

| Outcome | Meaning |
| --- | --- |
| Ready | Evidence is linked and residual risks are accepted. |
| Ready With Gaps | Pilot may proceed only with explicit risk ownership and review date. |
| Not Ready | Required evidence is missing or a critical risk remains unaccepted. |
