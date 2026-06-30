# Maintenance

> **Moved private (#684):** the detailed hosted-operator maintenance procedure now lives in the private `fairspot-platform` repository at `docs/runbooks/maintenance.md`.

This public page records the maintenance responsibility model. Exact update commands, platform schedules, operational contacts, and environment-specific rollback steps belong in the private platform runbook or a client-owned operations repository.

## Public Contract

| Area | Requirement |
| --- | --- |
| Updates | Runtime images, Dapr runtime, identity provider, gateway, state stores, and observability components are patched through a controlled release process. |
| Compatibility | Updates must preserve generated API contracts, Dapr component contracts, tenant isolation, and identity claim behavior. |
| Validation | Maintenance changes require relevant CI, smoke, backup/restore, and rollback evidence before customer traffic is considered healthy. |
| Rollback | Each hosted/client profile has a documented rollback path for application images and critical infrastructure changes. |
| Communication | Customer-impacting maintenance has defined notification, incident, and follow-up expectations. |

## Public References

- [Release Pipeline](./release-pipeline)
- [Incident Handling](./incident-handling)
- [Production](../production)
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary)
