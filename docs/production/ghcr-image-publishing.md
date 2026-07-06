# OPS021 — Publishing FairSpot Images to GHCR and Running NAS From Pulled Images

**Status:** Implemented.
**Tracks:** OPS021.
**Related:** [Release Pipeline](./release-pipeline.md) (OPS022), [NAS / Cloudflare Deployment Profile](./nas-cloudflare-deployment-profile.md), [`tools/start-container-stack.sh`](../../tools/start-container-stack.sh).

---

## Purpose

Make the NAS an **artifact consumer, not a build machine**. Service and web images are built once in CI and pushed to the GitHub Container Registry (GHCR); the NAS pulls and runs them. The NAS profile needs **only Docker and the Docker Compose v2 plugin** — no source build context, .NET SDK, Node, npm, or Dapr CLI.

The local developer flow is unchanged and still builds from source.

---

## What is published

The [`Publish Images`](../../.github/workflows/publish-images.yml) workflow builds and pushes ten images:

| Image | Source |
|---|---|
| `fairspot-audit`, `fairspot-booking`, `fairspot-configuration`, `fairspot-customer`, `fairspot-datahub`, `fairspot-identity`, `fairspot-notification`, `fairspot-profile`, `fairspot-reporting` | `code/server/Dockerfile` (shared multi-stage build, per-service build args) |
| `fairspot-web` | `code/web/fps-web/Dockerfile` (Vite build → nginx) |

All images are published under `ghcr.io/<owner>/<image>` (default owner `robertvejvoda`).

The workflow runs on pushes to `master`, on `v*` tags, and via manual dispatch (`workflow_dispatch`).

---

## Tag strategy

Each image is tagged:

| Tag | When | Use |
|---|---|---|
| `sha-<full-commit-sha>` | every build | **Immutable** — pin this on the NAS for repeatable deploys and rollback |
| `latest` | builds on the default branch (`master`) | convenience / "newest master" |
| `<release tag>` / semver (`1.2.3`, `1.2`, …) | on `v*` git tags | release pinning |

Pin a SHA tag on the NAS. `latest` is a moving target and should not be relied on for a reproducible evaluation.

---

## GHCR login (private packages)

GHCR packages may be private. On the NAS, authenticate Docker once with a GitHub Personal Access Token that has the `read:packages` scope:

```bash
echo "$GHCR_PAT" | docker login ghcr.io -u <github-username> --password-stdin
```

Store the PAT in the operator's secret manager, never in the repository or `.env`. Public packages need no login.

---

## NAS pull / run flow

The NAS profile uses the image-only compose file `docker-compose.services.images.yml` (no `build:` sections). The start script selects it automatically in `--nas` mode and pulls before starting:

```bash
# Select the registry and the immutable tag to deploy (in code/infrastructure/nas.env):
#   FPS_REGISTRY=ghcr.io/robertvejvoda
#   FPS_IMAGE_TAG=sha-<commit>

./tools/start-container-stack.sh --nas --env-file code/infrastructure/nas.env
```

Equivalent raw compose commands:

```bash
cd code/infrastructure
docker compose --env-file nas.env \
  -f docker-compose.yaml \
  -f docker-compose.services.images.yml \
  -f docker-compose.dapr.yml \
  -f docker-compose.nas.yml \
  -f docker-compose.services.nas.yml \
  pull
docker compose --env-file nas.env \
  -f docker-compose.yaml \
  -f docker-compose.services.images.yml \
  -f docker-compose.dapr.yml \
  -f docker-compose.nas.yml \
  -f docker-compose.services.nas.yml \
  up -d
```

`FPS_REGISTRY` (default `ghcr.io/robertvejvoda`) and `FPS_IMAGE_TAG` (default `latest`) select the images. The web container additionally reads `FPS_WEB_*` env vars to generate its runtime `/config.json` at startup.

### Environment variables (image mode)

Add these to `code/infrastructure/nas.env` (all optional — defaults shown):

| Variable | Default | Purpose |
|---|---|---|
| `FPS_REGISTRY` | `ghcr.io/robertvejvoda` | Registry + owner prefix for all FairSpot images |
| `FPS_IMAGE_TAG` | `latest` | Image tag to deploy — pin `sha-<commit>` for repeatable deploys/rollback |
| `FPS_WEB_API_BASE_URL` | _(unset → baked default)_ | Public API base URL the web app calls. With the single-origin model this is `https://app.<domain>/api` (nginx proxies `/api/` to Envoy) |
| `FPS_WEB_OIDC_AUTHORITY` | — | Public OIDC authority (e.g. `https://auth.fairspot.net/realms/fairspot`) |
| `FPS_WEB_OIDC_CLIENT_ID` | — | Web OIDC client id |
| `FPS_WEB_OIDC_REDIRECT_URI` | — | OIDC redirect URI |
| `FPS_WEB_OIDC_POST_LOGOUT_REDIRECT_URI` | — | OIDC post-logout redirect URI |
| `FPS_WEB_TENANT_NAME`, `FPS_WEB_PRODUCT_NAME`, `FPS_WEB_ENVIRONMENT` | `""` / `FairSpot` / `Production` | Branding / environment label |

When `FPS_WEB_API_BASE_URL` is set, the four OIDC values above are required (the web entrypoint fails closed if any is missing). When it is unset, the image serves the baked default `config.json` (localhost dev values).

> The `nas.env.example` template carries the credential variables only; the image-mode variables above are documented here and default safely, so the template is optional to edit for image mode.

---

## Rollback

Because every build is tagged by commit SHA, rollback is a tag change + redeploy:

```bash
# In code/infrastructure/nas.env, set FPS_IMAGE_TAG back to the previous good SHA:
#   FPS_IMAGE_TAG=sha-<previous-commit>

./tools/start-container-stack.sh --nas --env-file code/infrastructure/nas.env
```

The pull fetches the previous images and `up -d` recreates the containers. Named data volumes (MongoDB, PostgreSQL, Vault, etc.) are untouched, so durable state survives the rollback. No source checkout or rebuild is required on the NAS.

---

## Local developer flow (unchanged)

Local development still builds from source — nothing here changes it:

```bash
./tools/start-container-stack.sh           # builds code/server + uses docker-compose.services.yml
./tools/start-smoke-web.sh                 # Vite dev server for the web app
```

`--nas` is the only mode that switches to pulled images.

---

## Hosted routing (single origin)

The web container is the public entry point for the browser UI. nginx in `fairspot-web`:

- serves the SPA at `/` (client-side routing falls back to `index.html`), and
- reverse-proxies `/api/` to the Envoy gateway (`envoy-proxy:10000`).

Because the SPA and API share one origin, there is **no CORS** and no separate API hostname. Cloudflare public hostnames for the NAS profile:

| Hostname | Routes to | Serves |
|---|---|---|
| `app.<domain>` | `http://fairspot-web:80` | Web SPA + `/api/` proxy to Envoy |
| `auth.<domain>` | `http://keycloak:8080` | Keycloak public login |

Set the web app's API base URL to the same origin under `/api`:

```
FPS_WEB_API_BASE_URL=https://app.<domain>/api
FPS_WEB_OIDC_AUTHORITY=https://auth.<domain>/realms/fairspot
```

SPA routes live at `/` and never collide with the `/api/` backend prefix.

The NAS smoke run (`start-container-stack.sh --nas`) verifies `fairspot-web` is running and that `/` and `/config.json` are reachable.

## Out of scope (OPS021)

Production Vault/mTLS/storage-encryption hardening is **not** part of this slice. The Cloudflare hostname/Tunnel configuration that points `app.<domain>` at `fairspot-web` is an operator step documented in the [NAS / Cloudflare Deployment Profile](./nas-cloudflare-deployment-profile.md).
