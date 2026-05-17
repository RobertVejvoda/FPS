# SSO-First Customer Integration

FPS should integrate with a company primarily through the company's identity provider. For normal enterprise use, employees authenticate with SSO and FPS stores only the minimum profile and eligibility facts needed for booking, notification, audit, reporting, and support. FPS must not become a copy of the customer's HR or identity database.

Local FPS-created accounts are an explicit fallback for demo, small tenants, break-glass administration, or customers without SSO. When FPS owns such an account, credential verifiers such as password hashes are Secret data and must be handled by the Identity service using hardened credential storage. Plaintext passwords are never stored.

## Integration Goal

Customer integration should answer three questions:

| Question | Expected answer |
| --- | --- |
| Who is allowed to use FPS? | Active employee identity and tenant membership come from SSO/OIDC claims issued by the customer's IdP whenever possible. |
| What profile facts affect parking eligibility? | Vehicle, company-car, accessibility, location, and policy-related eligibility facts come from minimal IdP claims, authorized admin entry, employee self-service, or a narrowly scoped import where needed. |
| Which system owns the truth? | The customer IdP owns identity and login state. FPS stores only the mapped subject, tenant, role, and policy facts it needs to operate. |

## Candidate Integration Modes

| Mode | Use When | Data Shape | Notes |
| --- | --- | --- | --- |
| SSO/OIDC login | Normal company integration. | Stable IdP subject, tenant mapping, employee identifier where available, email if notifications need it, display name if the UI needs it, and group/role claims where available. | Primary model. FPS does not store the employee's company password and should not ask for it. |
| IdP claims and groups | Customer IdP can provide role, location, department, company-car, or policy-related claims. | Minimal mapped claims used by Profile, Identity, and Authorization. | Preferred source for lifecycle and authorization facts when the customer can maintain claims reliably. |
| SCIM provisioning | Customer supports standard user lifecycle provisioning. | User create/update/deactivate and group assignment. | Optional enterprise lifecycle support. It should complement SSO, not replace it or import a full HR record. |
| Manual admin entry | Demo, small tenant, local fallback account, or policy facts not available from IdP claims. | Tenant admin creates or corrects only required FPS profile facts. | Lowest integration cost, but privileged changes must be auditable. Local credentials are Secret if FPS owns login. |
| CSV or file bootstrap | First migration, low-volume correction, or customer cannot expose claims yet. | Validated file with only the required employee/profile fields. | Exceptional path. No passwords, tokens, or broad HR exports in files. Files need encryption and retention controls. |
| HR system export/API | Customer has a strong reason to sync profile facts outside IdP. | Narrow mapping from HR/fleet source into FPS profile schema. | Avoid unless justified. Requires data-processing review, scoped credentials, retries, idempotency, monitoring, and audit. |
| Custom API integration | Enterprise client requires a client-specific contract. | Client-specific integration behind an integration actor. | Future option only. Dapr bindings/adapters should keep provider details outside the domain model. |

## Minimum Data Contract

| Field | Required | Classification | Source of truth | Notes |
| --- | --- | --- | --- | --- |
| `tenantId` | Yes | Confidential | Trusted FPS tenant configuration / IdP mapping | Must come from trusted context, issuer mapping, or verified provisioning metadata, never arbitrary user input. |
| `externalSubject` | Yes for SSO users | Confidential | Customer IdP | Stable OIDC `sub` or equivalent external subject. This is the primary company-user mapping key. |
| `employeeId` | Optional unless policy requires it | Confidential | Customer IdP/HR | Use only when customer policy, support, or reporting needs an employee number. Prefer stable subject if employee ID is not required. |
| `userId` | Yes after onboarding | Confidential | FPS Identity | Internal FPS user key mapped to authenticated claims or a local FPS account. |
| `displayName` | Optional for v1 UI | Confidential | Customer IdP/admin/user | Avoid in events/audit where ID is enough. |
| `email` | Required only for email notification or login fallback | Confidential | Customer IdP/admin/user | Used by Notification; avoid in audit/event payloads unless required. |
| `activeStatus` | Yes | Confidential | Customer IdP/SCIM/admin | SSO token validity and provisioning state determine whether users can create new parking requests. |
| `roles` | Yes for admin/HR/auditor | Confidential | Customer IdP groups/admin | Role assignment must be auditable and tenant-scoped. |
| `homeLocationId` | Optional | Confidential | Customer IdP/admin/Profile | Used for default location and reporting filters. |
| `vehicles` | Policy-dependent | Confidential | Employee/Profile/HR | Includes license plate or operational vehicle identifier when required. |
| `hasCompanyCar` | Policy-dependent | Confidential | HR/fleet system | Affects Tier 1 allocation; changes must be auditable. |
| `accessibilityNeeds` | Policy-dependent | Confidential, sensitive | HR/authorized admin | Use minimum required flags, not medical detail. |
| `notificationPreferences` | Optional | Confidential | Employee/Profile | Mandatory operational notifications remain non-disableable. |
| `credentialVerifier` | Local accounts only | Secret | FPS Identity | Password hash or equivalent verifier for FPS-owned accounts only. Never imported from a company system and never stored as plaintext. |
| `refreshToken` | Optional | Secret | Customer IdP/FPS Identity | Store only if the selected auth flow requires it. Prefer short-lived access tokens and secure token storage. |

## Security and Privacy Rules

- SSO users authenticate with the customer IdP. FPS must not collect, import, store, log, or proxy the user's company password.
- FPS-local accounts are fallback accounts. Their credential verifier is **Secret** data and must use hardened password hashing, rotation/reset controls, and audit for administrative changes.
- Employee/profile data is **Confidential** unless it contains credentials, tokens, client secrets, API keys, or integration keys, which are **Secret**.
- Integration credentials are Secret and must be stored in the selected secret-management system, never in CSV files, GitHub issues, PRs, logs, screenshots, or documentation examples.
- Import files are exceptional. They must not contain passwords, tokens, broad HR extracts, or fields that FPS does not need. They must be encrypted at rest and deleted or retained according to customer-approved retention policy.
- Import previews and validation errors must mask Confidential fields where possible.
- Every SSO mapping, provisioning event, local-account creation, import, and manual correction must be tenant-scoped, idempotent where possible, and traceable to an actor or integration identity.
- Role, company-car, accessibility, active/inactive status, and vehicle eligibility changes must be auditable because they can affect allocation outcomes.
- Audit records should store stable IDs and reasons, not raw names, emails, or license plates unless explicitly required by a documented audit use case.
- GDPR rights requests must include FPS profile facts, mappings from external subjects or employee IDs to FPS users, local-account data where applicable, and any retained import files.

## Validation Rules

| Rule | Reason |
| --- | --- |
| Reject SSO/provisioning events without trusted issuer, tenant mapping, and stable external subject. | Prevent ambiguous identity and tenant injection. |
| Reject file rows without tenant, stable external subject or employee ID, and active/inactive status. | Prevent ambiguous identity and lifecycle state. |
| Reject files containing password, token, client secret, private key, or recovery-code fields. | Keep credentials out of import paths. |
| Validate role names against FPS role catalog. | Prevent accidental privilege creation. |
| Validate location and policy references against Configuration. | Prevent orphaned profile facts. |
| Validate vehicle fields only when policy requires vehicles. | Avoid collecting unnecessary data. |
| Detect duplicate external subjects and employee IDs per tenant. | Keep external mapping stable. |
| Require reason and actor for manual corrections to imported eligibility facts. | Preserve fairness and auditability. |
| Produce an import summary before file commit. | Let HR/admin review creates, updates, deactivations, and rejects. |

## Planned Slices

| Slice | Purpose | Notes |
| --- | --- | --- |
| `CUST002` SSO-First Customer Integration Contract | Define SSO mapping, minimal profile data, classification, local-account fallback, validation, and audit behavior. | Documentation/spec slice before implementation. |
| `P002` Profile Mapping And Minimal Facts | Implement profile mapping for SSO-derived users and the minimum policy facts needed by Booking. | File import is fallback/bootstrap, not the primary company integration. |
| `ID002` User Provisioning Integration | Map IdP subjects, claims, groups, roles, local-account fallback, and deactivation behavior into Identity. | SSO/OIDC first; SCIM optional for lifecycle where available. |
| `OPS005` Integration Secrets And Observability | Define secret handling, import logs, retry/error evidence, and metrics for integration actors. | Needed before customer-owned production integration. |

## Open Questions

- Which IdP should be tested first for demo/customer validation: Azure Entra ID, Okta, Google Workspace, or another provider?
- Which claims can a typical customer provide without custom HR integration?
- Is license plate required for v1 parking policy, or can vehicles be identified by type/capability only?
- Who is allowed to edit company-car and accessibility eligibility after import?
- Should employees self-maintain vehicles, or should HR/fleet data be authoritative?
- What import-file retention period is acceptable for demo and production when a file bootstrap is unavoidable?
