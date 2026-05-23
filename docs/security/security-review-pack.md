# Security and Privacy Review Pack

This document is the entry point for a security or DPO evaluation of FairSpot. It summarises the current security posture, maps controls to the relevant architecture and implementation evidence, and points to known gaps.

This is an architecture and product control document. It does not certify GDPR compliance or any security standard. Production use requires a legal review, controller/processor agreements, and client-specific configuration.

## Quick Reference

| Area | Status | Detail |
|------|--------|--------|
| Authentication | SSO-first OIDC | [Security Model](./security-model), [Authentication](./authentication) |
| Authorization | Tenant-scoped, role-based | [Authorization](./authorization), [Access Control](./access-control) |
| Tenant isolation | Enforced in every service | [Security Model](./security-model) § Data Layer |
| Data classification | 4 levels defined | [Security Model](./security-model) § Data Classification |
| Data privacy / GDPR alignment | Documented, not certified | [Data Privacy](./data-privacy) |
| Secret management | Dapr secretstore only | [Security Model](./security-model) § Secret data |
| Audit | Append-only, pseudonymised | [Audit](./audit), business layer [Audit](../business-layer/audit) |
| Encryption in transit | TLS at ingress | [Network Security](./network-security) |
| Encryption at rest | Delegated to infrastructure | Gap — see [Gap Register](./gap-register) |
| Observability / logging | Structured stdout + OTel traces | [Logging and Monitoring](./logging-monitoring) |
| Incident response | Runbook exists | [Incident Response](./incident-response) |
| BYOC responsibility split | Explicit | [Client Production Handoff](../production/client-production-handoff) |
| Known gaps | Documented | [Gap Register](./gap-register) |

---

## Authentication

FPS uses OIDC Authorization Code + PKCE for web and mobile clients. Services validate JWT bearer tokens using configurable `Auth:Authority` and `Auth:Audience` settings. Each service validates token signature, expiry, issuer, audience, tenant claim, and role claims independently — there is no internal auth proxy that can be bypassed by reaching a service directly.

**Local fallback**: A dev-token fallback path exists for local development and demo use. It is guarded by `devTokenFallbackEnabled` configuration, disabled by default in non-development environments, and excluded from production deployment profiles.

**Customer responsibility (BYOC)**:
- Operate and maintain the IdP (Keycloak, Azure AD, Okta, or equivalent).
- Configure OIDC client, realm, and group-to-role mappings.
- Manage user lifecycle: creation, deactivation, MFA policy.
- Issue short-lived access tokens (recommended: 15–60 minutes).

FPS does not store company passwords, MFA state, or IdP credentials.

---

## Authorization

All endpoints require a valid bearer token. Role claims are extracted from the token and matched against declared `[Authorize(Roles = "...")]` attributes. Tenant context is extracted from a `tenant_id` token claim — never from the request body or query string.

FPS roles: `employee`, `hr_manager`, `admin`, `report_viewer`, `auditor`. Roles are mapped from customer IdP groups via `TenantRoleMapping` configuration per tenant.

Privilege escalation paths are narrow: admin can create, modify, and disable tenant-scoped users, but admin credentials do not grant cross-tenant access or Secret data access.

---

## Tenant Isolation

Each service-owned store uses a tenant-safe key or collection partition derived from the authenticated `tenant_id` claim. Storage keys are never constructed from caller-supplied request bodies. Cross-tenant query paths do not exist in normal API flows.

Isolation is enforced at the application layer in every service. Infrastructure-layer isolation (separate databases per tenant) is listed in the gap register for production hardening.

---

## Data Classification and Handling

Four levels: Public, Internal, Confidential, Secret. Full definitions and examples are in the [Security Model](./security-model).

Key controls for Confidential data:
- Authenticated and authorized access only
- Tenant-scoped storage and queries
- Encryption in transit (TLS at Envoy ingress)
- PII fields masked in logs and events — no emails, names, license plates, or role weights in structured log output
- Audit records for sensitive reads/writes (admin actions, allocation outcomes, erasure requests)

Key controls for Secret data:
- Dapr secretstore only — no inline secrets in config files, YAML, or container environment unless documented as local-only
- No plaintext secrets in logs, events, git history, or GitHub issues
- Credential verifiers (local account password hashes) are Secret and stored in the Identity service only
- Access tokens are treated as Secret — not logged, not stored beyond session lifetime

---

## GDPR Alignment

FPS supports GDPR-aligned operation through these mechanisms:

| GDPR element | FPS implementation |
|-------------|-------------------|
| Data minimisation | Profile stores only fields needed for booking, allocation, notification, and reporting (no name/email unless provided by HR) |
| Purpose limitation | Confidential data is scoped per service contract; cross-service access uses defined APIs, not shared stores |
| Access rights | Employees see only their own bookings, profiles, and notifications |
| Rectification | Profile update via PUT /profile; booking cancellation via existing booking API |
| Erasure | `DELETE /audit/erasure/{userId}` pseudonymises audit actor; profile and booking data deletion documented (see gaps) |
| Audit accountability | Append-only audit log with actor, tenant, timestamp, reason for every sensitive action |
| Pseudonymisation | Audit records store `userId` (a token subject), not employee names; PII mapping resides separately in Profile service |
| Data portability | Reporting exports available to authorised roles; structured JSON/CSV format |
| DPA and residency | **Customer responsibility** — FPS does not sign DPAs or choose data residency; client operates the infrastructure |

---

## BYOC / Customer Responsibility Boundaries

The following are always customer responsibility in a client-owned deployment:

| Area | Customer owns |
|------|--------------|
| Infrastructure | Container hosting, networking, TLS certificates, storage provisioning |
| Identity provider | IdP operation, user lifecycle, MFA policy, group/role mapping configuration |
| Secret management | Secret store provisioning, rotation schedules, break-glass procedures |
| Encryption at rest | Storage-level encryption for all service data stores |
| Data backup and restore | Backup schedules, restore testing, RTO/RPO targets |
| DPA and legal | Controller/processor agreements, privacy notices, subprocessor list, breach notification |
| Log forwarding | Shipping container stdout to the client SIEM or log platform |
| Alerting | Threshold definition, on-call routing, escalation procedures |
| Production access | Privileged access policy, break-glass approval, time-bound access tracking |

Full split: [Client Production Handoff](../production/client-production-handoff).

---

## Audit

All sensitive actions — booking submission, allocation, cancellation, no-show, admin policy changes, audit erasure — produce append-only audit events via the Audit service. Audit records include: tenant ID, actor user ID (pseudonymised subject), action, resource, timestamp, and reason where applicable.

Audit access is restricted to `auditor` and `admin` roles. Raw PII mapping (connecting userId to a real name) requires a separate approved access path.

Audit retention and integrity evidence: gap — see [Gap Register](./gap-register).

---

## Observability and Logging

Services emit structured logs to stdout. Log output excludes: bearer tokens, passwords, raw PII (names, emails, license plates), Secret classification values, or hidden allocation internals.

OpenTelemetry trace export (OTLP) is implemented in the OBS001 baseline. Metrics and a local Grafana dashboard are planned in OBS002.

---

## Security Review Checklist for Client Evaluators

| Item | Check |
|------|-------|
| OIDC configuration | Authority and audience match the client IdP and deployment config |
| Role mapping | `TenantRoleMapping` covers all required customer groups |
| Secret store | All secrets use Dapr secretstore reference pattern, not inline values |
| TLS | TLS terminated at Envoy ingress; internal service-to-service uses Dapr mTLS in production |
| Tenant isolation | Verify no cross-tenant paths exist (spot-check customer service, profile, booking endpoints) |
| Audit | Verify audit records appear for admin actions and booking lifecycle events |
| Log inspection | Confirm no PII, tokens, or passwords appear in container stdout |
| Erasure | Test `DELETE /audit/erasure/{userId}` and verify profile/booking data handling |
| Backup/restore | Run restore drill per [Backup And Restore](../production/backup-restore) |
| Incident response | Review [Incident Response](./incident-response) and confirm contacts are populated |
