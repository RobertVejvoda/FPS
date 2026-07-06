# Keycloak realm import (hosted profile)

On the NAS/production profile, Keycloak starts with `--import-realm` and imports any
realm export placed in this directory (mounted read-only at
`/opt/keycloak/data/import`). Contents are **git-ignored** — realm exports can contain
client secrets and environment-specific redirect URIs, so they belong in the operator
secret store (`fairspot-platform`), never in this public repo.

## Produce a production realm export

From a configured Keycloak (with the correct `https://auth.<domain>` issuer and
`https://app.<domain>` redirect URIs for OPS012):

```bash
# export the realm from a running/offline Keycloak
kc.sh export --dir /opt/keycloak/data/export --realm fps-local --users realm_file
```

Copy the resulting `<realm>-realm.json` into this directory before deploying. Because
identity is Postgres-backed, once a realm exists it **persists across restarts** — the
import only bootstraps a fresh database.

The checked-in `keycloak/fps-local-realm.json` is the **local dev** realm (localhost/LAN
redirect URIs) and must not be used for a hosted deployment.
