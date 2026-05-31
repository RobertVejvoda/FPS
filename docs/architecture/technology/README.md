# Technology Architecture

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.3 |
| **Architecture State** | Target |
| **ADM Phase** | Phase D - Technology Architecture |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | Before hosted pilot |

Technology architecture defines the provider-neutral runtime, deployment, and operations model for FairSpot.

## Migration Status

Core technology direction has been restated from production and technology-layer evidence. It remains `Draft` because hosted smoke evidence, component hardening, backup/restore proof, and customer-ready persistence are not complete.

| Area | Status | Notes |
| --- | --- | --- |
| Runtime platform | Partial | Dapr-first runtime, service stack, persistence, ingress, and secrets boundaries are stated. |
| Deployment profiles | Partial | Local, NAS/Cloudflare hosted pilot, demo/cloud, and client-owned production profiles are stated. |
| Observability | Partial | Logs, metrics, traces, and business audit separation are stated. Retention and hosted evidence remain gaps. |
| Workflow and scheduled work | Partial | Draw workflow and schedule safety are stated. Broader scheduled jobs remain placeholders. |

## Requirement Interpretation

| Requirement | Technology Interpretation | Evidence | Gap |
| --- | --- | --- | --- |
| AR-003 / AR-014 | Hosted public surfaces must be exposed through the selected ingress/WAF profile and proven by smoke evidence. | [Deployment Profiles](/architecture/technology/deployment-profiles), [Observability](/architecture/technology/observability) | Hosted NAS/Cloudflare smoke evidence. |
| AR-009 | Dapr is the preferred runtime boundary for pub/sub, state, workflow, service invocation, secrets, resiliency, mTLS, component scopes, and outbox where supported. | [Runtime Platform](/architecture/technology/runtime-platform) | Hosted-profile Dapr hardening evidence. |
| AR-010 | Scheduled Draw and other recurring jobs must use deterministic keys and idempotent acquisition so multiple replicas do not execute the same work repeatedly. | [Runtime Platform](/architecture/technology/runtime-platform), [Deployment Profiles](/architecture/technology/deployment-profiles) | Workflow execution diagram and multi-instance test evidence. |
| AR-011 / AR-013 | Customer and DataHub persistence must be restart-safe and suitable for hosted pilot operation. | [Runtime Platform](/architecture/technology/runtime-platform), [Deployment Profiles](/architecture/technology/deployment-profiles) | Customer durable store and DataHub PostgreSQL projection store. |

## Legacy Evidence Disposition

| Legacy Source | Target Disposition |
| --- | --- |
| `technology-layer/**` | Source evidence for stack and service package history. Target runtime/deployment direction belongs here. |
| `production/**` | Operational runbooks and profile-specific evidence remain source evidence until the deployment/operations repository shape is mature enough to absorb them cleanly. |
| `production/dapr-first-production-standards.md` | Migrated directionally into [Runtime Platform](/architecture/technology/runtime-platform); keep as implementation/hardening source evidence. |
| `production/nas-cloudflare-deployment-profile.md` | Migrated directionally into [Deployment Profiles](/architecture/technology/deployment-profiles); keep as operational profile detail. |
| Local observability and smoke runbooks | Remain operational evidence linked from Technology and Security until hosted evidence is complete. |

## Contents

- [Runtime Platform](/architecture/technology/runtime-platform)
- [Deployment Profiles](/architecture/technology/deployment-profiles)
- [Observability](/architecture/technology/observability)

## Source Evidence

- [Technology Layer](/technology-layer)
- [Dapr-First Production Standards](/production/dapr-first-production-standards)
- [Hosting Strategy](/production/hosting-deployment-strategy)
- [NAS Cloudflare Deployment Profile](/production/nas-cloudflare-deployment-profile)
- [Draw Scheduling](/production/draw-scheduling-and-workflow)
