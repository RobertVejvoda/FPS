# FairSpot

[![CI](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/RobertVejvoda/FPS/actions/workflows/ci.yml)
[![Docs](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml/badge.svg?branch=master)](https://github.com/RobertVejvoda/FPS/actions/workflows/docs.yml)

FairSpot is an open-source, multi-tenant fair allocation platform for companies where demand for shared workplace resources exceeds supply. Parking is the first product module.

The product replaces manual email and spreadsheet coordination with a transparent booking and Draw process. Employees request parking, company-car obligations are handled first, and remaining spaces are allocated by documented fairness rules so access improves over time instead of depending on who emailed HR first.

The repository, service names, and some tooling still use `FPS` as the internal shorthand.

## What Works Today

- Backend services for Booking, Identity, Profile, Configuration, Notification, Audit, Reporting, and Customer/Tenant readiness.
- Employee booking lifecycle: submit, view, cancel, confirm usage, allocation status, notifications, and profile/vehicle data.
- Fair allocation rules for scarce parking, including company-car priority, weighted Draw behavior, penalties, and audit evidence.
- Audit, reporting, notification, and observability foundations for local demo and client evaluation.
- React web app and React Native/Expo mobile app paths for employee and admin-oriented evaluation.
- Local harness with seeded demo users, Keycloak, Dapr sidecars, gateway, metrics, logs, traces, and smoke commands.

FairSpot is still a product under active development. The current focus is demo readiness, client evaluation, privacy/audit hardening, and production handoff evidence.

## Choose Your Path

| I want to... | Start here |
| --- | --- |
| Understand the product | [Documentation site](https://www.vejvoda.net/FPS/) and [Client Evaluation Pack](./docs/client-evaluation-pack.md) |
| See the roadmap and status | [Roadmap](./docs/roadmap.md), [Implementation Tracker](./docs/implementation-tracker.md), and [Delivery Board](./docs/delivery-board.md) |
| Review the architecture | [Architecture Summary](./docs/architecture-views.md) and [Software Architecture](./docs/technology-layer/software-architecture.md) |
| Review security, privacy, and audit | [Security Review Pack](./docs/security/security-review-pack.md), [Security Model](./docs/security/security-model.md), and [Logging and Monitoring](./docs/security/logging-monitoring.md) |
| Run the local demo | [Local Test Harness](./docs/production/local-test-harness.md) and [Demo Seed Data](./docs/demo-seed-data.md) |
| Work on implementation | [GitHub Wiki](https://github.com/RobertVejvoda/FPS/wiki), [AGENTS.md](./AGENTS.md), and [Development Plan](./docs/development-plan.md) |

## Run The Local Demo

Prerequisites are documented in [Local Test Harness](./docs/production/local-test-harness.md). From the repository root:

```bash
./tools/start-local-harness.sh
```

In a second shell:

```bash
TOKEN=$(./tools/dev-auth.sh employee1)
curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/me
curl -H "Authorization: Bearer $TOKEN" http://localhost:10000/bookings
```

Useful local URLs after the harness starts:

| Tool | Use it when you want to... | URL |
| --- | --- | --- |
| API gateway | Call the local employee/admin API through the same gateway shape used by web and mobile. | http://localhost:10000 |
| Web app smoke path | Try the browser UI against the local gateway and seeded OIDC users. | `./tools/start-smoke-web.sh` then http://localhost:5200 |
| Mobile Expo smoke path | Try the React Native/Expo employee flow with the local backend. | `./tools/start-smoke-mobile.sh` |
| Grafana | See local service health, request rates, latency, alerts, and logs in one operations view. | http://localhost:3000 |
| Jaeger | Follow a request across services with distributed traces and `TraceId` correlation. | http://localhost:16686 |
| Prometheus | Inspect raw metrics, scrape targets, and alert rule state. | http://localhost:9090 |

Stop or reset:

```bash
./tools/stop-local-harness.sh
./tools/stop-local-harness.sh --reset
```

## Repository Structure

| Directory | Description |
| --- | --- |
| `code/` | Application source code for backend services, web, mobile, and infrastructure. |
| `docs/` | Product, architecture, security, production, and delivery documentation. Published at [vejvoda.net/FPS](https://www.vejvoda.net/FPS/). |
| `tools/` | Local harness, smoke, validation, seeding, auth, and generated-client scripts. |

## Documentation Model

- **GitHub README**: short project front door for first-time visitors.
- **Documentation site**: product, architecture, security, roadmap, demo, and client-evaluation material.
- **GitHub Wiki**: maintainer/operator workspace for implementation workflow, agent handoffs, troubleshooting, and runbooks.

## License

FairSpot is licensed under the GNU Affero General Public License v3.0 or later. See [LICENSE](./LICENSE).

Copyright and attribution notices are recorded in [NOTICE](./NOTICE). The FairSpot name and logo are project brand assets; forks, hosted offers, and commercial services must not imply official FairSpot status or Robert Vejvoda endorsement unless separately agreed. See the [FairSpot Brand Policy](./docs/strategy-layer/brand-policy.md).

Copyright (c) 2026 Robert Vejvoda.
