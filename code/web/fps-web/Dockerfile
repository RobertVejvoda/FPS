# syntax=docker/dockerfile:1
#
# Multi-stage build for the FairSpot web app (Vite + React SPA), served by nginx.
#
# The build context MUST be the repo `code/` directory, because the web app
# depends on the @robertvejvoda/fairspot-api-client TypeScript source via `file:../../clients/typescript`
# and a Vite alias to `../../clients/typescript/src`.
#
# Example:
#   docker build -f code/web/fps-web/Dockerfile -t fps-web:local code
#
# Runtime config: /config.json is generated from FPS_WEB_* env vars at container
# start (see docker-entrypoint.d/40-fps-config.sh); if no env is provided the
# default baked from public/config.json is served.

# ── Build stage ──────────────────────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# api-client source is referenced by the web app's file: dependency and Vite alias.
# Copy it first so `npm ci` can resolve the local dependency.
COPY clients/typescript ./clients/typescript

# Install web dependencies against the lockfile (cache-friendly: manifests first).
COPY web/fps-web/package.json web/fps-web/package-lock.json ./web/fps-web/
WORKDIR /app/web/fps-web
RUN npm ci

# Build the SPA (tsc --noEmit && vite build → dist/, including public/config.json).
COPY web/fps-web/ ./
RUN npm run build

# ── Runtime stage: nginx serving the static SPA ──────────────────────────────
FROM nginx:1.27-alpine AS runtime

# SPA-aware nginx config (history fallback + no-store on /config.json).
COPY web/fps-web/nginx.conf /etc/nginx/conf.d/default.conf

# Static assets (includes the default config.json baked from public/).
COPY --from=build /app/web/fps-web/dist /usr/share/nginx/html

# Runtime config generation runs before nginx starts (nginx:alpine entrypoint
# executes /docker-entrypoint.d/*.sh in order).
COPY web/fps-web/docker-entrypoint.sh /docker-entrypoint.d/40-fps-config.sh
RUN chmod +x /docker-entrypoint.d/40-fps-config.sh

EXPOSE 80
