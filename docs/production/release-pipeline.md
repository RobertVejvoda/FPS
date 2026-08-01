# OPS022 — Release CI/CD Pipeline and Deployment Promotion

**Status:** Implemented for Release 1.
**Tracks:** OPS022 (issue #623). Builds on [GHCR Image Publishing](./ghcr-image-publishing.md) (OPS021).
**Decision record:** `versions-and-decisions.md` → *Release artifact pipeline*.

---

## Principle

Release 1 and later hosted deployments **promote built artifacts**; they do not build on the runtime host. The pipeline is:

```
validate  →  build & publish  →  deploy selected tag  →  public smoke  →  evidence + rollback
   CI            CI (GHCR)          NAS (pull only)         scripted          recorded
```

Each stage maps to a concrete workflow or script:

| Stage | What runs | Where |
|---|---|---|
| **Validate** | `.github/workflows/ci.yml` — repo validation, NAS/DigitalOcean profile render and safety checks, API-client stale-check, mobile typecheck | every PR + push to `main` |
| **Build & publish** | `.github/workflows/publish-images.yml` — builds 9 server images + web, pushes immutable `sha-<commit>` (+ `latest` on `main`, release tag on `v*`) to GHCR | push to `main`, `v*` tags |
| **Deploy selected tag** | `tools/deploy-nas.sh --tag sha-<commit>` → `start-container-stack.sh --nas` pulls that tag, runs finite migration jobs, and starts/verifies the stack; the wrapper starts or attaches Cloudflare Tunnel | NAS host |
| **Public smoke** | exact app/auth hosts from CLI or ignored `nas.env`; checks web entry, `/config.json`, `/api/health/identity`, auth discovery, and protected surfaces; plus `tools/smoke-hosted.sh` for the full E2E | NAS host |
| **Evidence + rollback** | record deployed tag, smoke result, rollback tag, residual risks using [release-evidence-template.md](./release-evidence-template.md) | release notes / issue |

No secrets live in repository files. CI uses the built-in `GITHUB_TOKEN` for
GHCR. Public images need no NAS registry credential; a private registry would
use an operator-held read token. Deployment runs on the NAS, not a
GitHub-hosted runner, so NAS/Cloudflare/Vault credentials never enter CI.

The **DigitalOcean Droplet** profile runs the same deploy/smoke/rollback flow as the NAS rows above — the same pinned-tag pull, no host build — with `tools/deploy-digitalocean.sh` and `--digitalocean`, plus a delta overlay that suppresses public host ports for an internet-addressable host. Setup, operator access, and troubleshooting: [DigitalOcean Droplet setup](./digitalocean-setup.md).

---

## Which command to run

| Goal | Command |
|---|---|
| Local development (builds from source) | `./tools/local-start.sh` or `./tools/start-container-stack.sh` |
| NAS deploy with exact hosts from ignored `nas.env` | `./tools/deploy-nas.sh --tag sha-<commit> --existing-tunnel-container <name>` |
| NAS deploy with explicit CLI hosts | `./tools/deploy-nas.sh --app-host <app-host> --auth-host <auth-host> --tag sha-<commit>` |
| NAS internal troubleshooting (no public probe) | `./tools/start-container-stack.sh --nas --skip-public-smoke` |
| Re-run public smoke only | `./tools/start-container-stack.sh --nas --smoke-only --app-host <app-host> --auth-host <auth-host>` |
| DigitalOcean Droplet release deploy (NAS baseline + public-port suppression) | `./tools/deploy-digitalocean.sh --domain <domain> --tag sha-<commit>` — see [DigitalOcean setup](./digitalocean-setup.md) |
| DigitalOcean re-run public smoke only (non-mutating; never pulls/redeploys) | `./tools/start-container-stack.sh --digitalocean --domain <domain> --smoke-only` |

---

## Tag selection is explicit (no silent moving target)

`deploy-nas.sh` will **not** deploy the moving `latest` tag for a public (release-evidence) deployment. It requires an immutable tag:

```bash
# Release deploy — pinned, reproducible:
./tools/deploy-nas.sh --tag sha-<commit> --existing-tunnel-container <name>

# v* release tag also works:
./tools/deploy-nas.sh --tag v1.0.0 --existing-tunnel-container <name>
```

Without `--tag` (or with `--tag latest`) a public deploy aborts and tells you to pin a tag. `--allow-latest` overrides this for non-release experiments only; the deploy banner then warns the tag is not valid for Release 1 evidence. The chosen tag is printed in the deploy banner and must be copied into the release evidence.

---

## Promotion and rollback

Because every build is an immutable `sha-<commit>` image:

- **Promote** a validated commit by deploying its `sha-<commit>` tag to the NAS.
- **Roll back** by re-running `deploy-nas.sh ... --tag sha-<previous-good-commit>`. The pull fetches the prior images and `up -d` recreates the containers; named data volumes are untouched, so durable state survives. No rebuild on the host.

Record both the deployed tag and the designated rollback tag in the evidence so a rollback is a single command with a known-good target.

The DataHub migration launcher also keeps rollback to images published before
the explicit migration-and-exit mode finite: it runs the older image's existing
Development startup migration path on container loopback and stops it only
after ASP.NET reaches listening state. This makes the deployment workflow
compatible with older artifacts; it does not make an application image
compatible with a schema it cannot read. A schema-incompatible rollback still
requires the documented restore path or an explicit operational decision.

---

## What the public smoke verifies

`start-container-stack.sh --nas --app-host <app-host> --auth-host <auth-host>`
checks the exact configured names (Docker-only, via a throwaway curl container
with public egress):

1. **Web entry** — `https://<app-host>/` returns 200 (SPA index).
2. **Runtime config** — `https://<app-host>/config.json` has the exact expected API and OIDC values.
3. **API health** — `https://<app-host>/api/health/identity` is `Healthy`.
4. **Auth discovery** — `https://<auth-host>/realms/fairspot/.well-known/openid-configuration` resolves.
5. **Protected surface** — `https://<auth-host>/admin/` is **not** publicly reachable.

For the full hosted E2E (login → booking → notification → audit, TLS/WAF), run `tools/smoke-hosted.sh` with `APP_URL=https://app.<domain>/api`.

---

## Mobile release train

The pipeline above promotes **server/web** artifacts. Mobile is a launch-parity part of the customer launch path (see [hosted-mobile-build-plan.md](./hosted-mobile-build-plan.md)), and store distribution changes the release train in ways the image pipeline does not cover:

| Concern | Consequence for the release train |
|---|---|
| Version / build-number policy | Each store submission needs a unique, monotonically increasing build number (iOS `CFBundleVersion`, Android `versionCode`) alongside the marketing version. The version must be traceable to the commit/release tag that produced it. |
| EAS build profiles / channels | `production` builds map to the store track; `preview`/`development` map to internal validation. An update channel binds a build to the JS/OTA updates it may receive, so channel and profile are chosen per release, not ad hoc. |
| Release gates | A customer launch is not complete on server smoke alone: the store-readiness checklist and a passing device/beta validation are gates for the mobile half, unless an explicit waiver applies. |
| Store-review timing | App Store / Play review is asynchronous and can take days and can be rejected. Plan submission ahead of the target launch date; the server deploy and the store approval are coordinated, not simultaneous. |
| Rollback / waiver | Native binaries cannot be "rolled back" like container tags — recovery is a new build (or an OTA update within policy), or a temporary waiver to internal/beta distribution approved by Robert. Record which path was used. |
| Evidence | Record the submitted build number, the commit/tag it came from, store-review outcome, and any waiver in the release evidence, next to the server deploy evidence. |

Store credentials, account operations, signing material, and private submission evidence stay in the private `fairspot-platform` repository and its secret store — not in this public repo.

## Release evidence

After a release deploy, capture the evidence using [release-evidence-template.md](./release-evidence-template.md): deployed tag + commit, smoke pass/fail summary, the rollback tag, and any residual risks or accepted limitations. For a customer launch, include the mobile side: submitted build number + source commit/tag, store-review outcome, and any approved mobile waiver. Attach it to the release notes or the Release 1 validation issue (#388).
