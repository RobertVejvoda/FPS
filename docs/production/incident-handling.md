# Incident Handling

> **Moved private (#684):** the detailed hosted-operator incident procedure now lives in the private `fairspot-platform` repository at `docs/runbooks/incident-handling.md`.

This public page records the incident-management contract. Provider dashboards, exact escalation paths, private contacts, and operational commands belong in the private platform runbook or a client-owned operations repository.

## Public Contract

| Area | Requirement |
| --- | --- |
| Classification | Incidents are classified by customer impact, data protection risk, availability, integrity, and security scope. |
| Evidence | Operators preserve relevant audit records, technical logs, traces, metrics, deployment versions, and user-impact notes. |
| Communication | Customer-impacting incidents have a documented communication path, owner, and follow-up summary. |
| Recovery | Recovery actions use the selected deployment profile's backup, restore, rollback, and smoke-test evidence. |
| Follow-up | Post-incident actions feed architecture change control, security gaps, waivers, or implementation slices as appropriate. |

## Public References

- [Security Incident Response](../security/incident-response)
- [Audit](../security/audit)
- [Backup And Restore](./backup-restore)
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary)
