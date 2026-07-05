# Open-Core Documentation Boundary

> **Status:** classification slice (#670), first runbook consolidation (#684). This page defines which documentation stays in the **public open-core `fairspot` repository** and which belongs in the **private `fairspot-platform` repository**. Hosted-operator runbooks listed below have been moved private; public paths now keep stable summary/contract pages only.
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

These are hosted-operator / platform-control-plane / commercial-internal content. After #684, the first operator runbooks have moved to the private `fairspot-platform` repository and the public paths contain only stable summaries/replacements for customer and self-hoster readers.

The **Public summary / replacement** column records the public destination for each item — linked where a public summary exists, otherwise described as the summary that must accompany a future move.

| Doc (path under `docs/`) | Private home / status | Public summary / replacement |
|---|---|---|
| `production/nas-cloudflare-deployment-profile.md` | Moved to `fairspot-platform/docs/runbooks/nas-cloudflare-deployment-profile.md` in #684 | Public deployment contract at the same path; generic target in `architecture/technology/deployment-profiles` |
| `production/nas-cloudflare-auth-profile.md` | Moved to `fairspot-platform/docs/runbooks/nas-cloudflare-auth-profile.md` in #684 | Public OIDC integration contract at the same path |
| `production/hosted-smoke-runbook.md` | Moved to `fairspot-platform/docs/runbooks/hosted-smoke-runbook.md` in #684 | Public hosted readiness expectations at the same path |
| `production/backup-restore.md` | Moved to `fairspot-platform/docs/runbooks/backup-restore.md` in #684 | Public backup/restore responsibility contract at the same path |
| `production/nas-encryption-backup-evidence.md` | Moved to `fairspot-platform/docs/runbooks/nas-encryption-backup-evidence.md` in #684 | Public encryption & backup responsibility model at the same path |
| `production/maintenance.md` | Moved to `fairspot-platform/docs/runbooks/maintenance.md` in #684 | Public maintenance responsibility model at the same path |
| `production/incident-handling.md` | Moved to `fairspot-platform/docs/runbooks/incident-handling.md` in #684 | Public incident classification & customer-comms model at the same path |
| `production/monitoring.md` | Operator observability platform setup | Application observability contracts (OpenTelemetry/metrics/logs) |
| `production/hosting-deployment-strategy.md` | Platform deployment-profile choices | Customer deployment-options summary |
| `production/client-production-handoff.md` | Operator/customer responsibility-split internals | Customer handoff checklist |
| `production/release-pipeline.md`, `production/ghcr-image-publishing.md`, `production/release-evidence-template.md` | Operator release/promotion & image pipeline | Release versioning & support policy |
| `production/rto-rpo-requirements.md`, `production/perf001-readiness-evidence.md` | Platform SLA targets / capacity evidence | Customer-facing availability & performance expectations |
| `production/aws-setup.md`, `production/azure-setup.md` | Legacy cloud-vendor references only | Active public guidance is NAS/Cloudflare plus DigitalOcean target-cloud setup; client cloud choices stay provider-neutral and no pricing is promised |
| `production/demo-environment-baseline.md`, `production/demo-profile-decision.md`, `production/ops007-hosted-demo-evidence.md` | Hosted demo platform internals | Demo access/scope expectations |
| `production/customer-first-deployment-gap-analysis.md`, `production/cust008-onboarding-e2e-evidence.md`, `production/tenant-onboarding-smoke.md`, `production/integration-evidence.md`, `production/ops008-persistence-profile.md` | Onboarding/provisioning operator evidence | Customer onboarding process & checklist |
| `production/hosted-mobile-build-plan.md` | Hosted mobile build/release strategy | Mobile deployment contract |
| `production/testing-scenarios.md` | Operator platform test procedures | Customer test scenarios / evidence requirements |
| `security/cloudflare-waf-profile.md` | Cloudflare WAF / DDoS operator config | Security-profile expectations only |

> Secrets, tokens, Cloudflare account details, and credentials belong in **neither** repo's docs — they live only in the operator secret store.

## Shared packages (private-platform consumption)

The open core exposes a small, explicit set of packages the future private `fairspot-platform` repo can consume **without linking to FairSpot internals** (#673, PLAT009A):

| Package | Location | Surface | Consumed how |
|---|---|---|---|
| `FairSpot.SharedKernel` (NuGet) | `code/server/Shared/FPS.SharedKernel` | Identity/auth mechanism (multi-issuer JWT, claims, roles) + cross-cutting primitives | In-repo via `ProjectReference`; the platform service via GitHub Packages (NuGet). `dotnet pack` validated. |
| `@robertvejvoda/fairspot-api-client` (npm) | `code/clients/typescript` | Generated TS API types for the **customer/tenant** services only (identity, booking, profile, notification, customer) — **no** platform-plane endpoints | In-repo via `file:` dep + Vite alias / tsconfig path; private repos via GitHub Packages; pack validated with `npm pack --dry-run` |
| `@robertvejvoda/fairspot-ui` (npm) | `code/clients/ui` | Neutral, presentational UI primitives (e.g. `StatusBadge`) — **no** operator-console UI | Same `file:`-dep pattern as `@robertvejvoda/fairspot-api-client`; private repos via GitHub Packages; `npm pack --dry-run` validated |

Validation is wired into [`publish-packages.yml`](https://github.com/RobertVejvoda/fairspot/blob/master/.github/workflows/publish-packages.yml): PRs dry-run-pack the shared surfaces, and manual `workflow_dispatch` can publish to GitHub Packages when `dry_run` is false. **A future private repo references these by package, never by reaching into `code/` internals** — that boundary is the point of this slice.

## How to apply (for contributors)

- Adding hosted-operator or runbook detail? Put the public **contract/summary** in the public docs and keep operator specifics for `fairspot-platform`.
- Do not present platform-operator runbooks as default customer reading paths (see [Home → Reader Paths](../Home)).
- Do not invent pricing, SLAs, or legal terms; dual-license terms remain **TBD with legal** ([Licensing](./licensing)).
- When private-platform content moves, replace the open path with a public summary/contract rather than leaving a duplicate runbook.
