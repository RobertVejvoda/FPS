# Keycloak realm import (hosted profile)

On the NAS/production profile Keycloak starts with `--import-realm` and imports any realm
export in this directory (mounted read-only at `/opt/keycloak/data/import`).

## Versioned bootstrap: `fairspot-realm.json` (committed)

`fairspot-realm.json` is a **sanitized, versioned bootstrap** of the `fairspot` realm, so a
fresh deployment provisions identity automatically — no manual admin-console import. It is
safe to commit because it contains:

- **no users and no credentials**, and
- **public / PKCE clients only** (`fps-mobile`, `fps-web`) — no client secrets.

The mobile client uses the domain-independent `fairspot://login-callback` scheme. The web
client's redirect URI / web origin are parameterized with **`${FPS_APP_ORIGIN}`**, which
Keycloak substitutes from the container environment at import time (set `FPS_APP_ORIGIN`,
e.g. `https://app.example.net`, in `nas.env`). Because identity is Postgres-backed, the
import only bootstraps a fresh database; later changes persist across restarts.

Finalizing per-tenant details — additional redirect URIs, company-SSO / user-owned IdP
brokering, and any confidential clients — is OPS012 (public-domain auth) and belongs in the
operator secret store, not here.

## Operator-supplied realm exports (git-ignored)

Any other realm export you drop here (e.g. `kc.sh export` output) is **git-ignored** —
exports can carry client secrets and environment-specific config, so they live in the
operator secret store (`fairspot-platform`), never in this public repo.

The checked-in `keycloak/fps-local-realm.json` is the **local dev** realm (localhost/LAN
redirect URIs, demo users) and must not be used for a hosted deployment.
