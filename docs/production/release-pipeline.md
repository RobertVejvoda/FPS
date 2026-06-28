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
| **Validate** | `.github/workflows/ci.yml` — repo validation, API-client stale-check, mobile typecheck | every PR + push to master |
| **Build & publish** | `.github/workflows/publish-images.yml` — builds 9 server images + web, pushes immutable `sha-<commit>` (+ `latest` on master, release tag on `v*`) to GHCR | push to master, `v*` tags |
| **Deploy selected tag** | `tools/deploy-nas.sh --domain <d> --tag sha-<commit>` → `start-container-stack.sh --nas` pulls that tag (no build) and starts the stack + Cloudflare Tunnel | NAS host |
| **Public smoke** | `start-container-stack.sh --nas --domain <d>` (run by `deploy-nas.sh`): web entry, `/config.json`, `/api/health/identity`, auth discovery, protected-surface check; plus `tools/smoke-hosted.sh` for the full E2E | NAS host |
| **Evidence + rollback** | record deployed tag, smoke result, rollback tag, residual risks using [release-evidence-template.md](./release-evidence-template.md) | release notes / issue |

No secrets live in repository files. CI uses the built-in `GITHUB_TOKEN` for GHCR; the NAS authenticates with an operator-held PAT (`read:packages`) for private packages.

---

## Which command to run

| Goal | Command |
|---|---|
| Local development (builds from source) | `./tools/local-start.sh` or `./tools/start-container-stack.sh` |
| NAS release deploy (pulls a pinned tag) | `./tools/deploy-nas.sh --domain <domain> --tag sha-<commit>` |
| NAS internal troubleshooting (no public smoke) | `./tools/start-container-stack.sh --nas --skip-e2e` |
| Re-run public smoke only | `./tools/start-container-stack.sh --nas --domain <domain>` |

---

## Tag selection is explicit (no silent moving target)

`deploy-nas.sh` will **not** deploy the moving `latest` tag for a public (release-evidence) deployment. It requires an immutable tag:

```bash
# Release deploy — pinned, reproducible:
./tools/deploy-nas.sh --domain fairspot.net --tag sha-<commit>

# v* release tag also works:
./tools/deploy-nas.sh --domain fairspot.net --tag v1.0.0
```

Without `--tag` (or with `--tag latest`) a public deploy aborts and tells you to pin a tag. `--allow-latest` overrides this for non-release experiments only; the deploy banner then warns the tag is not valid for Release 1 evidence. The chosen tag is printed in the deploy banner and must be copied into the release evidence.

---

## Promotion and rollback

Because every build is an immutable `sha-<commit>` image:

- **Promote** a validated commit by deploying its `sha-<commit>` tag to the NAS.
- **Roll back** by re-running `deploy-nas.sh ... --tag sha-<previous-good-commit>`. The pull fetches the prior images and `up -d` recreates the containers; named data volumes are untouched, so durable state survives. No rebuild on the host.

Record both the deployed tag and the designated rollback tag in the evidence so a rollback is a single command with a known-good target.

---

## What the public smoke verifies

`start-container-stack.sh --nas --domain <domain>` checks (Docker-only, via a throwaway curl container with public egress):

1. **Web entry** — `https://app.<domain>/` returns 200 (SPA index).
2. **Runtime config** — `https://app.<domain>/config.json` is present and has `apiBaseUrl`.
3. **API health** — `https://app.<domain>/api/health/identity` is `Healthy` (proves the web `/api` proxy → Envoy → service path).
4. **Auth discovery** — `https://auth.<domain>/realms/fairspot/.well-known/openid-configuration` resolves.
5. **Protected surface** — `https://auth.<domain>/admin/` is **not** publicly reachable (must be 401/403/404; a 200 fails the smoke and points to the Cloudflare WAF/hostname rules, SEC010).

For the full hosted E2E (login → booking → notification → audit, TLS/WAF), run `tools/smoke-hosted.sh` with `APP_URL=https://app.<domain>/api`.

---

## Release evidence

After a release deploy, capture the evidence using [release-evidence-template.md](./release-evidence-template.md): deployed tag + commit, smoke pass/fail summary, the rollback tag, and any residual risks or accepted limitations. Attach it to the release notes or the Release 1 validation issue (#388).
