# Release Deployment Evidence — <release / date>

Copy this template per hosted deployment and fill it in. It is the record that a
specific, immutable artifact was deployed, smoke-tested, and is rollback-ready.
See [release-pipeline.md](./release-pipeline.md) for the pipeline.

## Deployment

| Field | Value |
|---|---|
| Date (UTC) | |
| Operator | |
| Profile | NAS / Cloudflare |
| Public hosts | `<app-host>` / `<auth-host>` / optional Access-protected `<ops-host>` |
| **Deployed image tag** | `sha-<commit>` (or `v<x.y.z>`) |
| Commit SHA | `<full 40-char sha>` |
| Registry | `ghcr.io/robertvejvoda` |
| Deploy command | `./tools/deploy-nas.sh --tag sha-<commit> [--existing-tunnel-container <name>]` |

## Validation evidence

| Check | Result | Notes |
|---|---|---|
| CI validate (PR/`main` green, including NAS profile) | ☐ pass / ☐ fail | run/commit link |
| Images published to GHCR | ☐ pass / ☐ fail | workflow run link |
| Stack started (health + sidecars) | ☐ pass / ☐ fail | |
| Web entry `<app-host>/` (200) | ☐ pass / ☐ fail | |
| `<app-host>/config.json` present and exact | ☐ pass / ☐ fail | apiBaseUrl = |
| `<app-host>/api/health/identity` Healthy | ☐ pass / ☐ fail | |
| Auth discovery `<auth-host>` | ☐ pass / ☐ fail | issuer = |
| Protected surface (Keycloak admin not public) | ☐ pass / ☐ fail | HTTP status = |
| Full hosted E2E (`smoke-hosted.sh`) | ☐ pass / ☐ fail / ☐ n/a | evidence file = |

## Rollback

| Field | Value |
|---|---|
| **Rollback tag** (previous known-good) | `sha-<previous-commit>` |
| Rollback command | `./tools/deploy-nas.sh --tag sha-<previous-commit> [--existing-tunnel-container <name>]` |
| Data volumes | preserved (rollback does not touch named volumes) |

## Residual risks / accepted limitations

- _List anything not fully verified or intentionally deferred for this release_
  _(e.g. WAF rate-limiting pending SEC010, persistence caveats, mobile internal-only)._

## Sign-off

| Role | Name | Decision |
|---|---|---|
| Operator | | ☐ deploy ok |
| Reviewer | | ☐ evidence accepted |
