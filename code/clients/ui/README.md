# @fps/ui

FairSpot shared **UI primitives** — neutral, presentational React components shared by the open web app (`code/web/fps-web`) and, later, the private `fairspot-platform` operator console.

This is an **open-core** package: keep it to design primitives only. Do not add platform-plane / operator-console UI here — that belongs in the private `fairspot-platform` repository (see [Open-Core Documentation Boundary](../../../docs/strategy-layer/open-core-boundary.md)).

## Components

- `StatusBadge` — colored status pill for booking/draw lifecycle states.

## Consumption

In-repo, `fps-web` consumes this as a local source package via a `file:` dependency plus a Vite alias and a TypeScript path (no build step — source `.tsx` is consumed directly), the same pattern as [`@fps/api-client`](../typescript/README.md):

```jsonc
// package.json
"dependencies": { "@fps/ui": "file:../../clients/ui" }
```

```ts
import { StatusBadge } from '@fps/ui';
```

A future private repo would reference it the same way (vendored/path) or via GitHub Packages once publishing is enabled. Validate packaging with `npm pack --dry-run`; typecheck with `npm run typecheck`.
