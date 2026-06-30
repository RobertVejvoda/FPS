# Hosted Smoke Runbook

> **Moved private (#684):** the detailed hosted-operator smoke runbook now lives in the private `fairspot-platform` repository at `docs/runbooks/hosted-smoke-runbook.md`.

This public page keeps the readiness expectations that customers, evaluators, and reviewers should understand. The executable hosted smoke commands, hostnames, operator-only checks, and evidence capture procedure are private platform operations material.

## Public Readiness Expectations

Before a hosted FairSpot environment is opened to external users, the operator must prove:

| Area | Expected evidence |
| --- | --- |
| Public boundary | Only intended public endpoints are reachable; internal service, Dapr, database, broker, and observability ports are not exposed. |
| Authentication | Web and mobile clients use the configured OIDC provider and tenant/user claims. |
| Core journey | Employee request, Draw/allocation, notification, HR/admin visibility, audit evidence, and reporting/data views work for seeded demo users. |
| Operations | Logs, traces, metrics, backup/restore evidence, and rollback/incident procedures are available to the operator. |
| Data protection | Hosted profiles use HTTPS, protected ingress, secret injection, and encrypted storage/backup controls before real customer data is processed. |

## Public References

- [Release 1 Validation Checklist](https://github.com/RobertVejvoda/fairspot/issues/388)
- [Production](../production)
- [Dapr-First Production Standards](./dapr-first-production-standards)
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary)
