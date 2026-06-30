# @fps/api-client

Generated TypeScript API client for FairSpot — typed request/response models scraped from each service's OpenAPI document. **Generated; do not edit by hand** (regenerate via `npm run generate`).

This is an **open-core** package surface. It exposes only the customer- and tenant-facing services:

- `identity`, `booking`, `profile`, `notification`, `customer`

It deliberately does **not** expose platform-plane / operator-console endpoints. Those belong to the future private `fairspot-platform` repository and must not be added to this open generated client (see [Open-Core Documentation Boundary](../../../docs/strategy-layer/open-core-boundary.md)).

## Consumption

In-repo, `fps-web` consumes this as a local source package via a `file:` dependency plus a Vite alias and a TypeScript path (no build step — the generated `.d.ts` is consumed directly):

```jsonc
// package.json
"dependencies": { "@fps/api-client": "file:../../clients/typescript" }
```

```ts
import type { paths } from '@fps/api-client/booking';
```

A future private repo would reference it the same way (vendored/path) or via GitHub Packages once publishing is enabled.

## Maintenance

- `npm run generate` — regenerate from running services (`tools/generate-api-client.sh`).
- `npm run check-stale` — verify the committed client matches the current OpenAPI.
- `npm pack --dry-run` — validate the package surface without publishing.
