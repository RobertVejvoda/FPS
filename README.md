# FairSpot

[![CI](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml)
[![Docs](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml/badge.svg?branch=master)](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml)

FairSpot is an open-source, multi-tenant fair allocation platform for companies where demand for shared workplace resources exceeds supply. Parking is the first product module.

The product replaces manual email and spreadsheet coordination with a transparent allocation process. Employees request parking, company-car obligations are handled first, and remaining spaces are assigned through a documented weighted Draw so access is fair over time instead of first-come, first-served.

The repository and service namespace still use `FPS` as the internal shorthand for now.

## Why It Exists

- Give employees a clear and explainable way to request scarce parking spaces.
- Reduce HR and facilities administration work.
- Keep allocation rules auditable across booking, notification, and audit services.
- Provide a reusable architecture for tenant-isolated workplace resource allocation.

## Start Here

- [Documentation site](https://www.vejvoda.net/FPS/)
- [Development plan](./docs/development-plan.md)
- [Business requirements](./docs/business-layer/requirements.md)
- [Software architecture](./docs/technology-layer/software-architecture.md)
- [Versions and decisions](./docs/versions-and-decisions.md)

## Structure

| Directory | Description |
|-----------|-------------|
| `code/` | Application source code |
| `docs/` | Architecture and documentation (also at [vejvoda.net/FPS](https://www.vejvoda.net/FPS/)) |

## Repository

https://github.com/RobertVejvoda/FPS

## License

FairSpot is licensed under the GNU Affero General Public License v3.0 or later.
See [LICENSE](./LICENSE).

Copyright (c) 2026 Robert Vejvoda.
