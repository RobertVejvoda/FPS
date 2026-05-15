Protects customer data and interactions by implementing strong authentication, encryption, and access controls to ensure privacy and security.
## Purpose

Customer security defines how tenant-level administration, onboarding, support, and configuration changes are controlled. The customer boundary is security-sensitive because it establishes who owns a tenant, who can administer users and policies, and which operational staff may act on behalf of that tenant.

## FPS Controls

- Tenant ownership must be created through an approved onboarding flow, not ad hoc database edits.
- Customer administrators may manage tenant users, roles, locations, parking policies, and configuration only within their tenant scope.
- Tenant identifiers come from authenticated context or trusted provisioning workflows; they must not be accepted from arbitrary request bodies for privileged operations.
- Role assignment, administrator changes, tenant configuration changes, and support access must be audited with actor, tenant, timestamp, reason where applicable, and correlation ID.
- Customer support must use least-privilege views. Access to employee-level confidential data requires a support case, tenant scope, and audit evidence.
- Tenant-managed secrets, integration credentials, and notification provider settings must be stored through masked or write-only interfaces where possible.

## Data Handling

Customer administration may process Confidential data such as tenant names, administrator identities, role assignments, locations, parking policies, billing contacts, and support records. It must not expose Secret data such as API keys, OAuth client secrets, database credentials, provider tokens, or private certificates.

Exports and support diagnostics must be tenant-scoped and must avoid unrelated employee booking history unless explicitly required for the support case or compliance request.

## Open Production Decisions

- Define the tenant onboarding approval workflow.
- Define customer administrator access review cadence.
- Define support access expiry and review process.
- Define tenant offboarding and data-retention procedures.
