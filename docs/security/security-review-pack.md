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
| Incident response | Runbook exists | [Incident Handling](../production/incident-handling) |
| BYOC responsibility split | Explicit | [Data Ownership and BYOC Boundaries](#data-ownership-and-byoc-boundaries) |
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
| Erasure | `DELETE /audit/pii-mappings/{userId}` pseudonymises audit actor; profile and booking data deletion documented (see gaps) |
| Audit accountability | Append-only audit log with actor, tenant, timestamp, reason for every sensitive action |
| Pseudonymisation | Audit records store `actor_hash` (SHA-256 of the token subject), not names; PII mapping (hash → identity) resides separately and is deletable via `DELETE /audit/pii-mappings/{userId}` |
| Data portability | Reporting exports available to authorised roles; structured JSON/CSV format |
| DPA and residency | **Customer responsibility** — FPS does not sign DPAs or choose data residency; client operates the infrastructure |

---

<!-- BYOC responsibility boundaries are covered in the detailed section below -->

---

## Audit

All sensitive actions — booking submission, allocation, cancellation, no-show, admin policy changes, audit erasure — produce append-only audit events via the Audit service. Audit records include: tenant ID, actor_hash (SHA-256 of the token subject — not the raw user ID), action, resource, timestamp, and reason where applicable.

Audit access is restricted to `auditor` and `admin` roles. Raw PII mapping (connecting userId to a real name) requires a separate approved access path.

Audit retention job (`DELETE /audit/retention`), integrity verification (`GET /audit/integrity`), and export (`GET /audit/export`) are implemented (A004/A005). Production retention schedules and periodic job scheduling remain client configuration responsibilities.

---

## Observability and Logging

Services emit structured logs to stdout. Log output excludes: bearer tokens, passwords, raw PII (names, emails, license plates), Secret classification values, or hidden allocation internals.

OpenTelemetry trace export (OTLP) is implemented (OBS001). Prometheus metrics and a local Grafana operations dashboard are implemented (OBS002). Alert rules for service down, high error rate, latency, and RabbitMQ are implemented (OBS003). See `docs/local-metrics-dashboard.md` and `docs/local-alerts-runbook.md`.

---

## Data Ownership and BYOC Boundaries

FairSpot is designed for client-owned operation where the client controls the infrastructure, data residency, and identity provider. This section clarifies what FairSpot stores versus what remains in the customer's systems.

### What FairSpot Stores (Confidential)

FairSpot stores the **minimum** tenant and employee data required for parking operations:

| Data | Purpose | Source | Classification |
|------|---------|--------|---------------|
| Tenant configuration | Policy, locations, spaces, capacity | Admin/Configuration API | Confidential |
| User profile identifiers | Subject mapping from IdP (`sub`), optional `employeeId` | SSO/OIDC claims or admin import | Confidential |
| Vehicle facts | License plate, vehicle type where policy requires | Employee self-service or HR import | Confidential |
| Eligibility flags | Company car, accessibility, home location | HR/admin or IdP claims | Confidential |
| Booking requests & outcomes | Request, allocation, cancellation, usage confirmation | Employee/Draw/System | Confidential |
| Notifications | In-app records, delivery metadata | Notification service | Confidential |
| Audit records | Action, actor hash, resource, timestamp, reason | All services | Confidential |
| Reporting projections | Aggregated usage, fairness metrics | Reporting service | Confidential |

**Key constraint**: FairSpot collects only what is necessary for parking operations, audit, notification, and reporting. It does not replicate the customer's full HR database.

### What Remains in the Customer IdP

The following **always** remain with the customer and are **never** stored in FairSpot:

| Data | Owner | Notes |
|------|-------|-------|
| Company passwords | Customer IdP | FairSpot validates tokens; it never sees or stores passwords. |
| MFA state and recovery codes | Customer IdP | MFA policy and enforcement are IdP responsibilities. |
| Full employee directory | Customer HR/IdP | FairSpot imports only mapped profile facts needed for policy. |
| Organizational structure | Customer HR | Department, manager, cost centre stay with the customer unless explicitly required for parking policy. |
| Employment contracts | Customer HR | Start date, termination, contract type are not FairSpot data unless policy explicitly needs them. |
| Payroll and compensation | Customer HR/Finance | Never imported or stored by FairSpot. |

### Pseudonymisation Strategy

FairSpot uses **pseudonymisation** to balance auditability with privacy:

- **Audit records** store `actor_hash` (SHA-256 of `userId`), not names or emails.
- **PII mapping** is stored separately in a mapping table that links `actor_hash` to real identity.
- **On GDPR erasure**: the PII mapping row is deleted; audit records remain immutable and anonymous.
- **Benefit**: Allocation fairness and audit evidence survive erasure while direct PII is removed.

This approach aligns with GDPR Article 25 (data protection by design) and Recital 26 (pseudonymisation as a safeguard).

---

## Retention Schedules

FairSpot requires explicit retention periods before production use. Implementation status varies by data type:

- **Audit**: `DELETE /audit/retention` is implemented (A004); client must configure the period and schedule invocation.
- **Bookings, notifications, security logs**: retention jobs are not yet implemented — these are documented gaps.
- All schedules: client is responsible for aligning periods with their legal basis and jurisdiction.

| Data Type | Recommended Retention | Deletion Method | Implementation Status |
|-----------|----------------------|-----------------|----------------------|
| **Booking requests (allocated)** | 1 year after booking date | Automated job deletes old booking aggregates | Gap — see [Gap Register](./gap-register) § GDPR |
| **Booking requests (rejected/cancelled)** | 90 days after final status | Automated job | Gap |
| **In-app notifications** | 90 days after creation | Automated job deletes old notification records | Gap |
| **Audit records (business actions)** | 7 years (or per jurisdiction) | `DELETE /audit/retention` (implemented, A004) | Implemented; client must configure retention period and schedule invocation |
| **Audit PII mapping** | Same as audit records, or shorter where erasure is requested | `DELETE /audit/pii-mappings/{userId}` (implemented) | Implemented; `DELETE /audit/retention` also covers PII mapping records when retention period is configured |
| **Security logs** | 1 year (or per incident retention policy) | Infrastructure log retention (client responsibility) | Client responsibility |
| **Reporting projections** | 2 years | Automated job or manual export + delete | Gap |
| **Backups** | 30 days rolling for operational backups; 7 years for compliance archives where required | Backup lifecycle policy in client infrastructure | Client responsibility |
| **Temporary import files** | Delete after processing or 7 days, whichever is shorter | Automated cleanup or manual admin action | Gap — not yet implemented |

**Client responsibility**: The retention schedule must align with the customer's legal basis, jurisdiction (GDPR, CCPA, local laws), and data processing agreement. FairSpot provides the deletion mechanisms; the client configures and enforces the schedule.

**Production-blocking**: Retention jobs for bookings, notifications, and audit records must be implemented or explicitly deferred with client approval before production use.

---

## Privileged and Break-Glass Access

FairSpot enforces least-privilege access by default. Administrative and break-glass access requires explicit justification, time-bounding, and audit.

### Privileged Roles

| Role | Scope | Permitted Actions | Audit Requirement |
|------|-------|------------------|------------------|
| `admin` | Tenant-scoped | Create/modify tenant config, users, roles, locations, spaces, policy | All admin actions audited with actor, tenant, resource, reason |
| `hr_manager` | Tenant + location-scoped | View/modify bookings for operational exceptions, manual allocation, penalty adjustment | Requires reason field; audited |
| `auditor` | Tenant-scoped, read-only | Query audit records, export reports | Audit access audited |
| `report_viewer` | Tenant-scoped, read-only | View aggregated reporting, fairness summaries | Not audited (operational read) |
| `employee` | Own data only | Request, cancel, confirm own bookings; view own notifications | Standard audit for booking lifecycle |

**No cross-tenant roles exist.** Admin for Tenant A cannot see or modify Tenant B data.

### Break-Glass Access (Production Operator / IT Support)

Break-glass access is **not a role** in FairSpot. It is an operational procedure for client IT when normal access is insufficient:

| Scenario | Access Method | Required Controls |
|----------|---------------|------------------|
| Database-level investigation | Direct database read access | Named operator, approval ticket, time-bound session (e.g. 2 hours), post-access review |
| Secret access (e.g. database connection string, API key) | Secret store admin access | Dual control where possible, approval, rotation after access, audit entry |
| Incident response (data corruption, security event) | Temporary elevated FairSpot admin role + database access | Incident ticket, executive/security approval, full action log, post-incident review |
| Backup restore | Restore script + approved backup file | Change request, tested procedure, validation checklist |

**Audit requirement**: All break-glass actions must be recorded in a client-maintained access log with:
- Operator identity
- Approval source (ticket ID, approver name)
- Reason and affected tenant/user/resource
- Timestamp and duration
- Actions taken
- Post-access follow-up (e.g., secret rotation, data validation)

**Secret rotation rule**: Any secret accessed by a human operator must be rotated immediately after the incident is resolved.

---

## Secret Management and Rotation

FairSpot uses **Dapr secretstore** as the abstraction boundary. The concrete secret store (Vault, Azure Key Vault, AWS Secrets Manager, etc.) is a client deployment choice.

### Secret Inventory

| Secret Type | Where Stored | Rotation Schedule | Rotation Method |
|-------------|--------------|------------------|-----------------|
| Database connection strings | Dapr secretstore | Quarterly or on exposure | Client rotates in secret store; FairSpot services restart to pick up new value |
| Message broker credentials | Dapr secretstore | Quarterly or on exposure | Client rotates; Dapr component restart |
| OIDC client secret (if required) | Dapr secretstore | Annually or on exposure | Client rotates at IdP and in secret store |
| Object storage credentials | Dapr secretstore | Quarterly or on exposure | Client rotates |
| Local account credential verifiers | Identity service database (hashed) | On user reset or security incident | Identity service API handles reset; hashes are never exported |
| Backup encryption keys | Client key management | Annually or on exposure | Client responsibility |
| CI/CD secrets (GitHub tokens, registry credentials) | GitHub Secrets or equivalent | Annually or on exposure | Delivery team rotates |

**Client responsibility**: Secret rotation schedules, automation, and monitoring are owned by the client. FairSpot provides the secretstore abstraction; it does not rotate secrets automatically.

**Production requirement**: A documented secret rotation runbook must exist before go-live, covering all production secrets and break-glass recovery scenarios.

---

## DPIA and Data Processing Agreement Inputs

FairSpot is an architecture and product; it does not certify GDPR compliance. A **Data Protection Impact Assessment (DPIA)** and **Data Processing Agreement (DPA)** are client/legal responsibilities. This section provides product inputs for those documents.

### DPIA Inputs

| DPIA Question | FairSpot Input |
|---------------|----------------|
| What personal data is processed? | Employee subject ID, optional employee ID, vehicle license plate (where policy requires), company-car flag, accessibility flag, home location, booking requests, allocation outcomes, notifications, audit actor hash. See [Data Privacy](./data-privacy). |
| What is the purpose? | Fair allocation of limited parking capacity, operational notifications, audit evidence, fairness reporting, tenant policy configuration. |
| What is the legal basis? | **Client determines legal basis.** FairSpot supports legitimate interest, consent, or contract performance depending on client policy. Privacy notice delivery is client responsibility. |
| Who has access? | Employees (own data), HR/facilities (tenant-scoped operational data), auditors (tenant audit records), admins (tenant config). No cross-tenant access. See [Security Model](./security-model) § Role to Data Access. |
| Where is data stored? | Client-controlled infrastructure. Data residency determined by client deployment region. FairSpot is provider-neutral; client chooses Azure, AWS, GCP, on-premises, etc. |
| How long is data retained? | Configurable retention periods. Recommended defaults: bookings 1 year, notifications 90 days, audit 7 years. Client enforces retention; FairSpot provides deletion mechanisms. See Retention Schedules above. |
| What are the risks? | Fairness perception if allocation internals leak; privacy risk if tenant isolation fails; security risk if secrets are exposed. Mitigations: pseudonymised audit, tenant-scoped queries, secret store, TLS, audit controls. See [Gap Register](./gap-register). |
| What safeguards are in place? | SSO-first (no company passwords stored), pseudonymised audit, tenant isolation, data minimisation, encryption in transit, secret management, role-based access, audit trails, GDPR erasure support. |

### DPA and Subprocessor Guidance

FairSpot is designed for **client-owned infrastructure** (BYOC). In this model:

- **Controller**: The client (employer/facilities owner).
- **Processor**: The client's IT operations or chosen managed-service provider.
- **FairSpot role**: Software supplier; not a processor unless FairSpot team operates infrastructure on client's behalf (not the current model).

**Subprocessors**: The client must list subprocessors in their DPA. Typical subprocessors in a FairSpot deployment:

| Service | Subprocessor Examples | Purpose |
|---------|----------------------|---------|
| Container hosting | Azure, AWS, GCP, or on-premises | Runtime environment |
| Database | MongoDB Atlas, AWS DocumentDB, Azure Cosmos DB, or self-hosted | Persistence |
| Identity provider | Azure AD, Okta, Keycloak (self-hosted) | Authentication |
| Secret store | HashiCorp Vault, Azure Key Vault, AWS Secrets Manager | Secret management |
| Object storage | Azure Blob, AWS S3, MinIO (self-hosted) | Exports, backups |
| Observability | Grafana Cloud, Datadog, Splunk, or self-hosted | Logs, metrics, traces |
| Email delivery (if enabled) | SendGrid, AWS SES, or SMTP relay | Notification delivery |

**Client responsibility**: Maintain the subprocessor list, sign DPAs with each subprocessor where required, and update privacy notices when subprocessors change.

---

## Implementation Gaps Blocking Production Use

The following gaps are documented in the [Gap Register](./gap-register) and must be resolved before production deployment:

### GDPR and Data Privacy

| Gap | Severity | Blocker? | Planned Resolution |
|-----|----------|----------|-------------------|
| Full employee data erasure path not implemented | High | **Yes** | Coordinated erasure flow across Profile, Booking, Notification, Audit services. Issue to be created. |
| Retention schedules not enforced | High | **Yes** | Automated retention jobs for bookings (1 year), notifications (90 days), audit (7 years or client policy). A004 exists for audit; booking/notification jobs to be sliced. |
| No consent or privacy notice flow | Medium | **No** (client UX/legal responsibility) | Client must implement at IdP or application layer. FairSpot does not display or record consent. |
| DPIA not completed | Medium | **Yes** (legal/client responsibility) | Client legal team completes DPIA using inputs from this document. |

### Security and Access Control

| Gap | Severity | Blocker? | Planned Resolution |
|-----|----------|----------|-------------------|
| Encryption at rest not configured | High | **Yes** | Client enables encryption on all stores. FairSpot delegates to infrastructure; no code change needed. |
| TLS for internal service-to-service traffic (Dapr mTLS) | Medium | **Yes** | Enable Dapr mTLS in production `fps-config.yaml`. Client responsibility. |
| Infrastructure-layer tenant isolation (shared stores) | Medium | **Yes** | Planned in OPS008. Current design uses application-layer tenant keys; production should use per-tenant collections or schemas. |
| No rate limiting on authentication endpoints | Medium | **Yes** | Envoy rate-limit policy to be configured before go-live. |
| No Web Application Firewall (WAF) | Medium | **Yes** | Client must add WAF or cloud-native DDoS protection in production. |

### Observability and Incident Response

| Gap | Severity | Blocker? | Planned Resolution |
|-----|----------|----------|-------------------|
| Prometheus metrics emitted but production wiring is client responsibility | Low | **No** | OBS002 implemented — GET /metrics on all services, Grafana dashboard provisioned. Client must configure log/metric forwarding to their platform. |
| Basic alert rules defined; production thresholds need tuning | Low | **No** | OBS003 implemented — FpsServiceDown, FpsHighErrorRate, FpsHighLatency, RabbitMQDown rules in place. Client must set production-appropriate thresholds and alerting destinations. |
| Log shipping to SIEM not configured | Medium | **No** (client responsibility) | Client configures log shipper (Fluent Bit, Fluentd, Splunk forwarder) to ship container stdout to SIEM. |

### Backup and Restore

| Gap | Severity | Blocker? | Planned Resolution |
|-----|----------|----------|-------------------|
| No tenant-scoped restore procedure tested | High | **Yes** | Document and test per-tenant restore from backup. Planned in backup-restore.md update. |
| Backup encryption key ownership undefined | Medium | **Yes** | Client must define key ownership and recovery process before first production backup. |

**Recommendation**: Treat all **"Yes"** blockers as mandatory pre-production tasks. Schedule a security and operations review with the client 4–6 weeks before planned go-live.

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
| Erasure | Test `DELETE /audit/pii-mappings/{userId}` and verify profile/booking data handling |
| Backup/restore | Run restore drill per [Backup And Restore](../production/backup-restore) |
| Incident response | Review [Incident Handling](../production/incident-handling) and confirm contacts are populated |
| Data ownership | Review "What FairSpot Stores" vs "What Remains in Customer IdP" table above |
| Retention schedules | Confirm client-approved retention periods and enforcement plan |
| Privileged access | Review break-glass procedure, operator access log, and secret rotation rules |
| DPIA inputs | Provide DPIA inputs table to client legal/DPO for assessment |
| Production gaps | Review [Gap Register](./gap-register) and confirm all blockers have resolution plans |
