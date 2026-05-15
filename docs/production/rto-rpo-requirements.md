# RTO/RPO Requirements

RTO defines how quickly a capability should be restored. RPO defines how much data loss is acceptable. These targets are planning values for the first hosted pilot and should be reviewed before a paid production rollout.

## Initial Targets

| Capability | RTO | RPO | Notes |
| --- | --- | --- | --- |
| Public docs | 4 hours | 24 hours | Documentation can be restored from Git. |
| Mobile/web app availability | 2 hours | 0 for deployed artifact | Re-deploy known-good build from CI/CD. |
| Identity provider | 2 hours | 24 hours for configuration | User/session disruption is expected during identity outage. |
| Booking write model | 1 hour | 15 minutes target, 24 hours maximum for pilot | Booking is the core product state and needs the strongest database recovery. |
| Booking read model | 4 hours | Rebuildable from write state/events | Read models may be rebuilt if authoritative state is intact. |
| Notification records | 4 hours | 1 hour | Missed external delivery may need retry or compensating message. |
| Audit records | 1 hour | Near-zero for accepted auditable mutations | Auditable mutations should not be accepted if reliable audit persistence is unavailable. |
| Reporting projections | 24 hours | Rebuildable | Reporting can lag and rebuild. |
| Configuration/policy | 2 hours | 15 minutes target | Active policy affects Booking decisions. |
| Object storage exports | 24 hours | 24 hours | Exports can often be regenerated unless used as compliance evidence. |
| Observability stack | 8 hours | 24 hours | Product can run without dashboards, but incident visibility is reduced. |

## Recovery Classes

| Class | Examples | Recovery expectation |
| --- | --- | --- |
| Authoritative state | Booking, Configuration, Customer, Audit PII mapping | Restore from tested backups; validate consistency before reopening writes. |
| Derived state | Reporting projections, cache, some read models | Rebuild from authoritative state or events. |
| Operational state | Queues, metrics, traces, logs | Recover enough to resume safe operation and incident analysis. |
| Deployment state | Images, manifests, Dapr components, ingress config | Recreate from repository, registry, and deployment pipeline. |
| Secret state | Keys, tokens, certificates, connection strings | Restore or rotate through approved secret-management process. |

## Review Triggers

Revisit these targets when:

- FPS moves from pilot to paid tenant use;
- billing or payment data becomes production scope;
- customers require contractual SLA/SLO commitments;
- multi-region deployment becomes a real requirement;
- legal/compliance review changes audit or retention obligations.
