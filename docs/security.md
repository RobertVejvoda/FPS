# Security

Security is a top-level FPS concern because it cuts across product policy, tenant isolation, implementation, operations, and compliance. This section defines the controls that every layer must respect.

Start with [Security Model](./security/security-model). It is the source of truth for actors, roles, data classification, data-in-transit and data-at-rest controls, protocols, encryption, secret access tracking, and GDPR alignment.

## Identity and Access

- [Authentication](./security/authentication): how users, services, and integrations prove identity using OIDC/OAuth, JWTs, PKCE, service identities, and token-handling rules.
- [Authorization](./security/authorization): how FPS decides which authenticated actor may perform an action, including tenant scope, role checks, fail-closed behavior, and service-level permissions.
- [Access Control](./security/access-control): operational least-privilege controls, privileged access, MFA expectations, access reviews, and break-glass constraints.

## Data Protection

- [Security Model](./security/security-model): the complete FPS actor, role, data classification, data layer, protocol, encryption, secret tracking, and GDPR alignment baseline.
- [Confidentiality](./security/confidentiality): controls that prevent unauthorized disclosure of tenant, employee, booking, policy, audit, billing, and operational data.
- [Data Privacy](./security/data-privacy): GDPR-aligned processing rules, data minimisation, rights handling, retention planning, and pseudonymisation expectations.
- [Encryption](./security/encryption): encryption requirements for clients, services, pub/sub, databases, backups, object storage, telemetry, and secret stores.
- [Integrity](./security/integrity): controls that protect booking decisions, audit records, events, policies, and reports from unauthorized or accidental modification.
- [Traceability](./security/traceability): correlation, audit, and evidence requirements for user actions, system actions, privileged access, and data exports.

## Operations and Compliance

- [Audit](./security/audit): security audit evidence, event capture, review responsibilities, and the split between business audit and security/secret-access audit.
- [Logging and Monitoring](./security/logging-monitoring): observability requirements that detect suspicious activity without logging secrets or raw confidential payloads.
- [Incident Response](./security/incident-response): response flow for security incidents, data exposure, suspected tenant leakage, and secret compromise.
- [Compliance](./security/compliance): regulatory and customer-assurance controls, including GDPR, SOC-style evidence, PCI considerations where billing evolves, and audit readiness.
- [Backup and Recovery](./security/backup-recovery): backup, restore, retention, tenant-scoped recovery, and recovery validation expectations.
- [Vulnerability Management](./security/vulnerability-management): dependency, container, infrastructure, configuration, and application vulnerability handling.
- [Security Patching](./security/security-patching): patch cadence, ownership, emergency patching, and verification expectations.
- [Security Awareness Training](./security/security-awareness-training): human-process controls for developers, operators, administrators, and support users.
- [Environments](./security/environments): environment separation, sanitized test data, production access restrictions, and development safeguards.
- [CI/CD](./security/cicd): pipeline permissions, secrets handling, review gates, artifact provenance, and deployment controls.

## Architecture and Network

- [Microservice Security Patterns](./security/microservice-security-patterns): API gateway, Dapr service invocation, Dapr mTLS, pub/sub, service identity, and defense-in-depth patterns.
- [Network Security](./security/network-security): ingress, internal service exposure, broker/database boundaries, TLS, firewall policy, and Kubernetes network segmentation.

## Client and Service Notes

- [Web App](./security/web-app): browser-facing controls for session handling, XSS/CSRF protection, API use, and safe display of confidential data.
- [Mobile App](./security/mobile-app): mobile token storage, PKCE login, secure storage, offline limitations, and device-level risk controls.
- [Customer](./security/customer): tenant administration, onboarding, role assignment, policy ownership, support boundaries, and customer-facing data responsibilities.
- [Notification](./security/notification): notification payload limits, channel security, user targeting, delivery failure handling, and provider credential handling.
- [Feedback](./security/feedback): safe handling of user-submitted feedback, attachments, support diagnostics, and moderation of accidental secret or PII submission.
