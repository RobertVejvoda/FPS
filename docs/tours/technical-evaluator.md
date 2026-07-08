# Technical Evaluator Tour

**Who this is for:** a technical evaluator or self-hosted operator working out how FairSpot runs, how it would plug into your environment, and what evidence exists.

**What matters to you:** a real local/container path you can run, how the demo data is built, where the docs live, and the line between evaluation and client-owned production.

## Run it locally

The whole stack runs as containers — services, Dapr sidecars, gateway, identity, and stores — needing only Docker and Compose on the host (no host .NET SDK or Dapr CLI):

```bash
./tools/start-container-stack.sh --seed
```

That brings the stack up and seeds the Green Logistics showcase. Reach it at the API gateway (`http://localhost:10000`), the web app (`./tools/start-smoke-web.sh` → `http://localhost:5200`), the Expo mobile app (`./tools/start-smoke-mobile.sh`), and Keycloak (`http://localhost:8180`, realm `fps-local`). The self-hosting details are in the [Local Test Harness](../production/local-test-harness) and [Dapr-First Standards](../production/dapr-first-production-standards).

## How it fits your environment

- **Portability boundaries.** Dapr is the component portability boundary (pub/sub, state, secrets, bindings, service invocation) and OpenTelemetry is the observability boundary — so provider choices (broker, store, secret manager, IdP, exporter) stay behind stable seams. See [Technology Architecture](../architecture/technology/) and [Deployment Profiles](../architecture/technology/deployment-profiles).
- **Services.** Bounded services — Booking, Identity/Profile, Notification, Audit, Configuration, Reporting — with explicit integration boundaries. Map them in [Information Systems](../architecture/information-systems/) and the [Service Catalog](../architecture/information-systems/service-catalog).
- **Observability.** OpenTelemetry-compatible metrics, logs, and traces exist for the demo request path without exposing Secret data ([Observability](../architecture/technology/observability)).

## The demo-seed story

`--seed` builds a small, synthetic, narrative-driven tenant (Green Logistics): ten employees plus HR/admin/report/auditor role users, a `GL-HQ` parking location with six labelled slots, ten requests dated past the Draw cutoff, then a Draw that mixes allocations and waitlists, a company-car Tier-1 allocation, and a cancellation that promotes the next fair employee. It is verifiable end to end. Full detail: [Demo Seed Data](../demo-seed-data).

## These docs

This site is [Docsify](../tooling) — Markdown pages served from `docs/`, no build step. Preview locally with `npx docsify-cli serve docs`. The [Architecture Repository](../architecture/) is TOGAF-structured; [Views and Diagrams](../architecture/views/) catalogues the diagram sources and rendered images.

## The evaluation / production boundary

- **Release 1 is synthetic/demo evaluation only** and is not approved for real customer data unless explicitly agreed. The [Release 1 Scope](../roadmap#release-1-scope) records what is ready, demo-only, and deferred.
- **Client-owned production** is the current direction: the client owns the platform, IdP, persistence, secrets, observability, backups, and incident process. FairSpot provides the architecture, contracts, and evidence — the public [Operations](../production) pages hold the responsibility contracts; detailed hosted-operator runbooks live in the private `fairspot-platform` companion.
- Backup/restore, encryption evidence, and incident handling have public **contracts** here ([Backup and Restore Contract](../production/backup-restore), [Operational Evidence Checklist](../production/operational-evidence-checklist)); the hosted execution and recorded evidence are private.

## Go deeper

- [Local Test Harness](../production/local-test-harness) and [Dapr-First Standards](../production/dapr-first-production-standards).
- [Deployment Profiles](../architecture/technology/deployment-profiles) and [NAS / Cloudflare Deployment Contract](../production/nas-cloudflare-deployment-profile).
- [Client Evaluation Pack](../client-evaluation-pack) — the architecture, deployment, and cost summary.
