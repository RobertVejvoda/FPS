# Technology Architecture

| Field | Value |
| --- | --- |
| Status | Draft |
| Version | 0.1 |
| Architecture State | Target |
| ADM Phase | Phase D - Technology Architecture |
| Responsible | Codex/Product Owner |
| Accountable | Robert |
| Last Reviewed | - |
| Next Review | Before hosted pilot |

Technology architecture defines the provider-neutral runtime, deployment, and operations model for FairSpot.

## Migration Status

Core technology direction has been restated from production and technology-layer evidence. It remains `Draft` because hosted smoke evidence, component hardening, backup/restore proof, and customer-ready persistence are not complete.

| Area | Status | Notes |
| --- | --- | --- |
| Runtime platform | Partial | Dapr-first runtime, service stack, persistence, ingress, and secrets boundaries are stated. |
| Deployment profiles | Partial | Local, NAS/Cloudflare hosted pilot, demo/cloud, and client-owned production profiles are stated. |
| Observability | Partial | Logs, metrics, traces, and business audit separation are stated. Retention and hosted evidence remain gaps. |
| Workflow and scheduled work | Partial | Draw workflow and schedule safety are stated. Broader scheduled jobs remain placeholders. |

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
