# Open-Core Documentation Boundary

> **Status:** classification slice (#670). This page defines which documentation stays in the **public open-core `fairspot` repository** and which moves to the **private `fairspot-platform` repository** later. It **classifies and redirects only** — no docs are moved or deleted in this slice.
>
> Companions: [Licensing](./licensing), [Commercialisation](./commercialisation), [Evaluation & Onboarding](./evaluation-and-onboarding). Source decisions: open/private repo split (#660), platform epic (#633), licensing decision (#642).

## The model

FairSpot follows an open-core split (the Dapr / Diagrid pattern — see [Evaluation & Onboarding §1](./evaluation-and-onboarding)):

- **Public `fairspot` (AGPL):** the runtime + fairness engine + tenant self-administration, together with all customer-facing and architectural documentation. Anyone can self-host a single organisation and inspect how allocation works.
- **Private `fairspot-platform` (commercial, later):** the hosted operator product — cross-tenant operations, the platform console, hosted-deployment runbooks, sales/onboarding queue internals, cost/usage metering, and sensitive operating procedures.

**The commercial line is the platform plane, not the fairness engine.** The open core stays good enough to evaluate and run a normal tenant; see [Commercialisation → Free and open-core boundary](./commercialisation) and [Licensing](./licensing).

## What stays public

| Area | Examples |
|---|---|
| Product overview & evaluation | `Home`, `roadmap`, `client-evaluation-pack`, `demo-and-evaluation`, `demo-seed-data` |
| Architecture (entire repository) | `architecture/**` — vision, principles, business/, information-systems/, technology/, security/, governance/, views/ |
| Domain docs | `business-layer/**`, `application-layer/**`, `technology-layer/**` |
| Security model | `security/**` (model, controls, privacy, authn/z, audit, compliance) — except the platform edge config noted below |
| Tenant self-hosting & runtime contracts | `production/local-test-harness`, `production/dapr-first-production-standards`, `production/tenant-storage-contract`, `production/availability-model`, `production/draw-scheduling-and-workflow`, dev/test setup |
| License, commercial frame & brand | `strategy-layer/licensing`, `commercialisation`, `brand-policy`, `core-values`, `approach`, `evaluation-and-onboarding` |
| Public API / runtime contracts | OpenAPI, generated client contracts, integration/event catalog |

## What moves private later (migration inventory)

These are **candidates** — hosted-operator / platform-control-plane / commercial-internal content. They stay in the public repo **for now** (this slice deletes nothing); when the private repo lands (#660) each row moves there and a public summary/replacement keeps what a customer or self-hoster actually needs.

The **Public summary / replacement** column records the *planned* public destination for each item — linked where a public doc already exists, otherwise described as the summary that will accompany the move. This slice classifies and redirects; it does not yet write the full replacement docs.

| Doc (current path under `docs/`) | Why it is private-later | Public summary / replacement |
|---|---|---|
| `production/nas-cloudflare-deployment-profile.md` | Our NAS + Cloudflare-tunnel operator deployment specifics | Generic containerized self-hosting guidance (`architecture/technology/deployment-profiles`) |
| `production/nas-cloudflare-auth-profile.md` | Platform Envoy / OIDC gateway config | Public OIDC integration contract & requirements |
| `production/hosted-smoke-runbook.md` | Operator smoke procedure for the hosted instance | Customer-facing readiness expectations |
| `production/backup-restore.md` | Operator backup/restore procedures | Backup/restore responsibility contract |
| `production/nas-encryption-backup-evidence.md` | NAS-specific encryption/backup operator evidence | Encryption & backup responsibility model |
| `production/maintenance.md` | Operator maintenance procedures | Maintenance responsibility model |
| `production/monitoring.md` | Operator observability platform setup | Application observability contracts (OpenTelemetry/metrics/logs) |
| `production/incident-handling.md` | Operator incident-response playbook | Incident classification & customer-comms model |
| `production/hosting-deployment-strategy.md` | Platform deployment-profile choices | Customer deployment-options summary |
| `production/client-production-handoff.md` | Operator/customer responsibility-split internals | Customer handoff checklist |
| `production/release-pipeline.md`, `production/ghcr-image-publishing.md`, `production/release-evidence-template.md` | Operator release/promotion & image pipeline | Release versioning & support policy |
| `production/rto-rpo-requirements.md`, `production/perf001-readiness-evidence.md` | Platform SLA targets / capacity evidence | Customer-facing availability & performance expectations |
| `production/aws-setup.md`, `production/azure-setup.md` | Cloud-vendor operator/cost references | Cloud-choice guidance (no pricing) |
| `production/demo-environment-baseline.md`, `production/demo-profile-decision.md`, `production/ops007-hosted-demo-evidence.md` | Hosted demo platform internals | Demo access/scope expectations |
| `production/customer-first-deployment-gap-analysis.md`, `production/cust008-onboarding-e2e-evidence.md`, `production/tenant-onboarding-smoke.md`, `production/integration-evidence.md`, `production/ops008-persistence-profile.md` | Onboarding/provisioning operator evidence | Customer onboarding process & checklist |
| `production/hosted-mobile-build-plan.md` | Hosted mobile build/release strategy | Mobile deployment contract |
| `production/testing-scenarios.md` | Operator platform test procedures | Customer test scenarios / evidence requirements |
| `security/cloudflare-waf-profile.md` | Cloudflare WAF / DDoS operator config | Security-profile expectations only |

> Secrets, tokens, Cloudflare account details, and credentials belong in **neither** repo's docs — they live only in the private operator vault (`fairspot-ops`).

## Shared packages (private-platform consumption)

The open core exposes a small, explicit set of packages the future private `fairspot-platform` repo can consume **without linking to FairSpot internals** (#673, PLAT009A):

| Package | Location | Surface | Consumed how |
|---|---|---|---|
| `FairSpot.SharedKernel` (NuGet) | `code/server/Shared/FPS.SharedKernel` | Identity/auth mechanism (multi-issuer JWT, claims, roles) + cross-cutting primitives | In-repo via `ProjectReference`; the platform service via GitHub Packages (NuGet). `dotnet pack` validated. |
| `@fps/api-client` (npm) | `code/clients/typescript` | Generated TS API types for the **customer/tenant** services only (identity, booking, profile, notification, customer) — **no** platform-plane endpoints | In-repo via `file:` dep + Vite alias / tsconfig path; pack validated with `npm pack --dry-run` |
| `@fps/ui` (npm) | `code/clients/ui` | Neutral, presentational UI primitives (e.g. `StatusBadge`) — **no** operator-console UI | Same `file:`-dep pattern as `@fps/api-client`; `npm pack --dry-run` validated |

Validation is wired into [`publish-packages.yml`](https://github.com/RobertVejvoda/fairspot/blob/master/.github/workflows/publish-packages.yml) (`workflow_dispatch`): it packs `FairSpot.SharedKernel` and dry-run-packs both npm packages. NuGet can publish to GitHub Packages with the built-in token; npm publishing stays manual/disabled (both packages are `private:true` and consumed via `file:` deps today). **A future private repo references these by package, never by reaching into `code/` internals** — that boundary is the point of this slice.

## How to apply (for contributors)

- Adding hosted-operator or runbook detail? Put the public **contract/summary** in the public docs and keep operator specifics for `fairspot-platform`.
- Do not present platform-operator runbooks as default customer reading paths (see [Home → Reader Paths](../Home)).
- Do not invent pricing, SLAs, or legal terms; dual-license terms remain **TBD with legal** ([Licensing](./licensing)).
- When the private repo lands (#660), move the rows above and replace each with its public summary.
