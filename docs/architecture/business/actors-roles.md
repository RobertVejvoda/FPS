# Actors And Roles

Roles are business responsibilities, not implementation permissions. Permission details belong in security and application architecture.

| Actor / Role | Responsibilities | Default Viewpoint | Concerns | Status | Source Evidence |
| --- | --- | --- | --- | --- | --- |
| Employee | Request parking, view outcome, cancel own request where allowed, confirm usage, manage vehicle/profile facts. | Employee Home / My Spots. | Simple workflow, clear timing, clear outcome reasons, privacy, no hidden technical terms. | Partial | [Personas](/business-layer/personas), [Roles](/business-layer/roles), [My Spots UX](/business-layer/my-spots-ux) |
| HR / Facility Manager | Manage operational request queues, inspect pending/allocated/cancelled state, see next Draw time, run authorized Draw action, cancel any tenant-scoped request with reason. | HR operations workspace. | Workload, auditable actions, employee notifications, safe exception handling, no cross-tenant leakage. | Placeholder | [Roles](/business-layer/roles), [Role Intent Roadmap](/business-layer/role-intent-roadmap) |
| Facilities Coordinator | Maintain the physical capacity model: locations, zones, spaces, capacity pools, temporary closures, EV/accessibility/company-car capabilities, and resource-map publication. | Resource map operations workspace. | Accurate capacity, safe publication, utilization by zone/capability, audit of map changes. | Placeholder | [Personas](/business-layer/personas), [Role Intent Roadmap](/business-layer/role-intent-roadmap), [Business Requirements](/business-layer/requirements) |
| Tenant Administrator | Configure tenant setup, locations, policy, roles, identity mapping, readiness, and seed/imported employee facts. | Tenant administration workspace. | Setup clarity, tenant isolation, policy correctness, readiness evidence. | Placeholder | [Tenant Onboarding](/business-layer/tenant-onboarding), [Customer Data Import](/business-layer/customer-data-import) |
| System Administrator / Operator | Operate platform/runtime, observe health, manage deployment profile, support recovery, and view platform-level status without acting as a tenant employee. | Platform operations workspace. | Availability, backup/recovery, observability, secrets, WAF/ingress, Dapr runtime. | Placeholder | [Production](/production), [Technology Architecture](/architecture/technology/) |
| Auditor / Compliance Reviewer | Review allocation decisions, privileged actions, sensitive access, and policy-change evidence. | Audit/evidence workspace. | Integrity, retention, least privilege, privacy, reproducibility. | Partial | [Audit](/business-layer/audit), [Security](/security), [Gap Register](/security/gap-register) |
| Client IT / Security Reviewer | Assess deployment, identity, network, WAF, secrets, backup, and operational risk. | Deployment/security review. | Attack surface, tenant isolation, operational responsibility, recovery. | Partial | [Production](/production), [Security Architecture](/architecture/security/) |
| Customer Sponsor / Executive Sponsor | Decide pilot readiness, risk acceptance, target scope, commercial direction, and whether parking outcomes justify broader rollout. | Customer readiness and management summary. | Value, risk, customer-first scope, deferred features, HR effort avoided, utilization, fairness trend, cost. | Partial | [Strategy](/strategy), [Roadmap](/roadmap), [Client Evaluation Pack](/client-evaluation-pack), [Personas](/business-layer/personas) |
| Product Owner / Architecture Owner | Maintain architecture direction, acceptance criteria, delivery priorities, and decisions. | Architecture repository and delivery board. | Consistency, customer readiness, implementation clarity. | Partial | [Versions and Decisions](/versions-and-decisions), [Delivery Board](/delivery-board) |

## Role Separation Rules

- Employee, HR, tenant administrator, and system administrator must not share the same default experience.
- Facilities users may manage resource maps and closures, but allocation decisions still follow approved Booking/Configuration policy and audited publication.
- HR can cancel tenant-scoped requests only with an auditable reason and employee notification where the employee is affected.
- Employees must not trigger a Draw directly.
- HR/facility Draw triggers must be controlled, idempotent, tenant-scoped, and audited.
- Auditor views may show deeper decision evidence than employee views, but must still avoid unrelated personal data.
- System administrator views are operational and must not become a shortcut around tenant authorization.

## Open Role Placeholders

- Exact HR dashboard content and actions need implementation validation.
- Exact facilities resource-map workspace and publication workflow need implementation validation.
- Exact tenant administrator readiness workflow needs implementation validation.
- Exact system administrator default view needs implementation validation.
- Architecture RACI is governed in [RACI](/architecture/governance/raci).
