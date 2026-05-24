# SSO-First Customer Integration

FairSpot should integrate with a company primarily through the company's identity provider. For normal enterprise use, employees authenticate with SSO and FairSpot stores only the minimum profile and eligibility facts needed for booking, notification, audit, reporting, and support. FairSpot must not become a copy of the customer's HR or identity database.

Use [Tenant Onboarding](./tenant-onboarding) for the end-to-end sequence that creates a new company tenant, configures identity, creates the first administrator, sets up parking policy/slots, loads pilot employee facts, and proves readiness.

Local FairSpot-created accounts are an explicit fallback for demo, small tenants, break-glass administration, or customers without SSO. When FairSpot owns such an account, credential verifiers such as password hashes are Secret data and must be handled by the Identity service using hardened credential storage. Plaintext passwords are never stored.

## Integration Goal

Customer integration should answer three questions:

| Question | Expected answer |
| --- | --- |
| Who is allowed to use FairSpot? | Active employee identity and tenant membership come from SSO/OIDC claims issued by the customer's IdP whenever possible. |
| What profile facts affect parking eligibility? | Vehicle, company-car, accessibility, location, and policy-related eligibility facts come from minimal IdP claims, authorized admin entry, employee self-service, or a narrowly scoped import where needed. |
| Which system owns the truth? | The customer IdP owns identity and login state. FairSpot stores only the mapped subject, tenant, role, and policy facts it needs to operate. |

## Contract Boundaries

This contract defines what FairSpot expects from customer identity and profile integration. It is intentionally provider-neutral: Azure Entra ID, Keycloak, Okta, Google Workspace, and other OIDC-compatible providers can be used if they can emit the required claims or be mapped through trusted tenant configuration.

| Boundary | Contract |
| --- | --- |
| Authentication | Company employees authenticate through the customer IdP using OIDC/OAuth 2.0. FairSpot validates signed tokens and never handles the company password. |
| Tenant resolution | `tenantId` comes from trusted issuer-to-tenant configuration, a trusted `tenant_id` claim emitted by the IdP, or verified provisioning metadata. FairSpot must not accept tenant identity from arbitrary request bodies. |
| User resolution | SSO users are mapped by `(tenantId, issuer, externalSubject)`, where `externalSubject` is the stable OIDC `sub` or equivalent immutable subject. |
| Authorization | FairSpot roles come from mapped IdP groups/roles or tenant-admin assignments. Role mapping is tenant-scoped, auditable, and does not create roles dynamically from untrusted claims. |
| Profile facts | FairSpot stores only facts required for parking policy, notification, audit, reporting, and support. Broad HR records stay outside FairSpot. |
| Local accounts | FairSpot-local accounts are fallback accounts only. Their credential verifiers are Secret data owned by Identity and are not imported from customer systems. |
| Provisioning | SCIM or file/bootstrap import may create, update, or deactivate users and profile facts, but SSO remains the normal login and identity proof. |
| Audit | Integration decisions must be attributable to a human actor, FairSpot system actor, or named customer integration identity. |

## OIDC Tenant And Issuer Rules

Each tenant integration must define a trusted issuer contract before users can authenticate:

| Rule | Requirement |
| --- | --- |
| Trusted issuer | The token `iss` must match a configured issuer for the tenant or a configured multi-tenant issuer mapping. Unknown issuers fail closed. |
| Audience | The token `aud` must match the FairSpot API/client audience expected for that environment. |
| Signature and expiry | JWT signature, expiry, not-before, and standard validation rules must pass before claims are read. |
| Tenant mapping | Tenant resolution must be deterministic. If both issuer mapping and `tenant_id` claim exist, they must agree or authentication fails. |
| Subject mapping | The token must contain a stable subject. For OIDC this is `sub`; provider-specific identifiers may be used only if documented as immutable for that tenant. |
| Role/group mapping | Raw IdP groups are mapped to FairSpot roles through tenant configuration. Unmapped groups are ignored unless the tenant configuration explicitly rejects them. |
| Deactivation | A user who can no longer authenticate through the trusted IdP, or is marked inactive by trusted provisioning/admin state, must not be able to create new parking requests. Existing booking lifecycle handling remains governed by Booking rules. |

FairSpot services consume only authenticated context after token validation. Employee-facing APIs must not accept caller-supplied tenant, user, or role values as replacements for authenticated context.

## Candidate Integration Modes

| Mode | Use When | Data Shape | Notes |
| --- | --- | --- | --- |
| SSO/OIDC login | Normal company integration. | Stable IdP subject, tenant mapping, employee identifier where available, email if notifications need it, display name if the UI needs it, and group/role claims where available. | Primary model. FairSpot does not store the employee's company password and should not ask for it. |
| IdP claims and groups | Customer IdP can provide role, location, department, company-car, or policy-related claims. | Minimal mapped claims used by Profile, Identity, and Authorization. | Preferred source for lifecycle and authorization facts when the customer can maintain claims reliably. |
| SCIM provisioning | Customer supports standard user lifecycle provisioning. | User create/update/deactivate and group assignment. | Optional enterprise lifecycle support. It should complement SSO, not replace it or import a full HR record. |
| Manual admin entry | Demo, small tenant, local fallback account, or policy facts not available from IdP claims. | Tenant admin creates or corrects only required FairSpot profile facts. | Lowest integration cost, but privileged changes must be auditable. Local credentials are Secret if FairSpot owns login. |
| CSV or file bootstrap | First migration, low-volume correction, or customer cannot expose claims yet. | Validated file with only the required employee/profile fields. | Exceptional path. No passwords, tokens, or broad HR exports in files. Files need encryption and retention controls. |
| HR system export/API | Customer has a strong reason to sync profile facts outside IdP. | Narrow mapping from HR/fleet source into FairSpot profile schema. | Avoid unless justified. Requires data-processing review, scoped credentials, retries, idempotency, monitoring, and audit. |
| Custom API integration | Enterprise client requires a client-specific contract. | Client-specific integration behind an integration actor. | Future option only. Dapr bindings/adapters should keep provider details outside the domain model. |

## Minimum Data Contract

| Field | Required | Classification | Source of truth | Notes |
| --- | --- | --- | --- | --- |
| `tenantId` | Yes | Confidential | Trusted FairSpot tenant configuration / IdP mapping | Must come from trusted context, issuer mapping, or verified provisioning metadata, never arbitrary user input. |
| `externalSubject` | Yes for SSO users | Confidential | Customer IdP | Stable OIDC `sub` or equivalent external subject. This is the primary company-user mapping key. |
| `employeeId` | Optional unless policy requires it | Confidential | Customer IdP/HR | Use only when customer policy, support, or reporting needs an employee number. Prefer stable subject if employee ID is not required. |
| `userId` | Yes after onboarding | Confidential | FairSpot Identity | Internal FairSpot user key mapped to authenticated claims or a local FairSpot account. |
| `displayName` | Optional for v1 UI | Confidential | Customer IdP/admin/user | Avoid in events/audit where ID is enough. |
| `email` | Required only for email notification or login fallback | Confidential | Customer IdP/admin/user | Used by Notification; avoid in audit/event payloads unless required. |
| `activeStatus` | Yes | Confidential | Customer IdP/SCIM/admin | SSO token validity and provisioning state determine whether users can create new parking requests. |
| `roles` | Yes for admin/HR/auditor | Confidential | Customer IdP groups/admin | Role assignment must be auditable and tenant-scoped. |
| `homeLocationId` | Optional | Confidential | Customer IdP/admin/Profile | Used for default location and reporting filters. |
| `vehicles` | Policy-dependent | Confidential | Employee/Profile/HR | Includes license plate or operational vehicle identifier when required. |
| `hasCompanyCar` | Policy-dependent | Confidential | HR/fleet system | Affects Tier 1 allocation; changes must be auditable. |
| `accessibilityNeeds` | Policy-dependent | Confidential, sensitive | HR/authorized admin | Use minimum required flags, not medical detail. |
| `notificationPreferences` | Optional | Confidential | Employee/Profile | Mandatory operational notifications remain non-disableable. |
| `credentialVerifier` | Local accounts only | Secret | FairSpot Identity | Password hash or equivalent verifier for FairSpot-owned accounts only. Never imported from a company system and never stored as plaintext. |
| `refreshToken` | Optional | Secret | Customer IdP/FairSpot Identity | Store only if the selected auth flow requires it. Prefer short-lived access tokens and secure token storage. |

## Source-Of-Truth Rules

When the same fact can come from multiple systems, FairSpot uses the following precedence:

| Fact | Preferred source | Fallback source | Rule |
| --- | --- | --- | --- |
| Login permission | Customer IdP | FairSpot-local account for fallback users | SSO users must prove identity through the IdP. Local users prove identity through FairSpot Identity only when explicitly created as fallback accounts. |
| Tenant membership | Trusted issuer mapping or tenant claim | Verified provisioning/admin assignment | Conflicting tenant mappings fail closed. |
| FairSpot roles | Mapped IdP groups/roles | Tenant admin assignment | Privileged role changes require audit and must be tenant-scoped. |
| Active/inactive status | IdP/SCIM lifecycle | Tenant admin correction | Inactive users cannot create new requests. Corrections require actor and reason. |
| Vehicle facts | Employee self-service or HR/fleet source, depending on tenant policy | Tenant admin correction | Store only fields required by policy. |
| Company-car eligibility | HR/fleet source | Authorized tenant admin correction | Changes affect fairness and require audit. |
| Accessibility eligibility | Authorized HR/admin source | Authorized correction | Store minimum operational flag only, not medical detail. |
| Notification address | IdP claim or employee/admin profile | Local account email | Required only where email notification is enabled or local login needs it. |

## Local Account Fallback

Local FairSpot accounts are allowed for demo users, small tenants without SSO, break-glass administration, and explicitly approved fallback scenarios. They are not the default customer integration model.

Local-account rules:

- Local accounts must be tenant-scoped and visibly distinguishable from SSO-mapped users in admin/support views.
- FairSpot Identity owns password hashing, reset, lockout, and credential-verifier storage for local accounts.
- Credential verifiers, reset tokens, recovery codes, and equivalent material are Secret data.
- Customer passwords, IdP passwords, and password hashes from external systems must not be imported into FairSpot.
- Creating, disabling, resetting, or privilege-changing a local account requires audit with actor, reason, tenant, target user, and timestamp.
- Break-glass accounts should be few, named, periodically reviewed, and disabled when no longer required.

## File And Bootstrap Import Contract

File/bootstrap import is an exception path for first setup or low-volume correction. It is not a broad HR-data ingestion model.

Accepted file-import properties:

- tenant-scoped input;
- stable external subject or employee identifier;
- active/inactive status;
- only the profile facts needed by FairSpot policy and notification behavior;
- import preview before commit;
- idempotent create/update/deactivate behavior where possible;
- encrypted storage while processing;
- retention or deletion according to customer-approved policy.

Rejected file-import content:

- passwords, password hashes, recovery codes, tokens, client secrets, private keys, or API keys;
- broad HR exports unrelated to parking policy;
- medical details beyond minimum operational accessibility eligibility;
- arbitrary tenant IDs that are not verified against trusted tenant configuration;
- role names that are not mapped to the FairSpot role catalog.

## Security and Privacy Rules

- SSO users authenticate with the customer IdP. FairSpot must not collect, import, store, log, or proxy the user's company password.
- FairSpot-local accounts are fallback accounts. Their credential verifier is **Secret** data and must use hardened password hashing, rotation/reset controls, and audit for administrative changes.
- Employee/profile data is **Confidential** unless it contains credentials, tokens, client secrets, API keys, or integration keys, which are **Secret**.
- Integration credentials are Secret and must be stored in the selected secret-management system, never in CSV files, GitHub issues, PRs, logs, screenshots, or documentation examples.
- Import files are exceptional. They must not contain passwords, tokens, broad HR extracts, or fields that FairSpot does not need. They must be encrypted at rest and deleted or retained according to customer-approved retention policy.
- Import previews and validation errors must mask Confidential fields where possible.
- Every SSO mapping, provisioning event, local-account creation, import, and manual correction must be tenant-scoped, idempotent where possible, and traceable to an actor or integration identity.
- Role, company-car, accessibility, active/inactive status, and vehicle eligibility changes must be auditable because they can affect allocation outcomes.
- Audit records should store stable IDs and reasons, not raw names, emails, or license plates unless explicitly required by a documented audit use case.
- GDPR rights requests must include FairSpot profile facts, mappings from external subjects or employee IDs to FairSpot users, local-account data where applicable, and any retained import files.

## Validation Rules

| Rule | Reason |
| --- | --- |
| Reject SSO/provisioning events without trusted issuer, tenant mapping, and stable external subject. | Prevent ambiguous identity and tenant injection. |
| Reject file rows without tenant, stable external subject or employee ID, and active/inactive status. | Prevent ambiguous identity and lifecycle state. |
| Reject files containing password, token, client secret, private key, or recovery-code fields. | Keep credentials out of import paths. |
| Validate role names against FairSpot role catalog. | Prevent accidental privilege creation. |
| Validate location and policy references against Configuration. | Prevent orphaned profile facts. |
| Validate vehicle fields only when policy requires vehicles. | Avoid collecting unnecessary data. |
| Detect duplicate external subjects and employee IDs per tenant. | Keep external mapping stable. |
| Require reason and actor for manual corrections to imported eligibility facts. | Preserve fairness and auditability. |
| Produce an import summary before file commit. | Let HR/admin review creates, updates, deactivations, and rejects. |

## Downstream Slice Acceptance Criteria

Future implementation slices that consume this contract must preserve these constraints:

| Slice | Acceptance criteria |
| --- | --- |
| `P002` Profile Mapping And Minimal Facts | Stores only mapped profile facts required for policy, notification, audit, reporting, and support; records source and last-updated evidence for policy-sensitive facts; rejects ambiguous tenant/user mappings. |
| `ID002` User Provisioning Integration | Maps `(tenantId, issuer, externalSubject)` to FairSpot users; validates trusted issuer/audience/subject; applies configured group-to-role mapping; handles inactive users fail-closed; keeps local-account credential handling inside Identity. |
| `OPS005` Integration Secrets And Observability | Stores integration credentials only through the selected secret-management path; emits safe metrics/logs for success, rejection, retries, and validation failures without leaking Confidential or Secret data. |
| `CUST001` Tenant Onboarding | Creates tenant integration configuration before live SSO users can authenticate; records trusted issuer, audience, tenant mapping, role mapping, and fallback-account policy. |
| `CUST007` Tenant Readiness Check | Verifies tenant setup, identity, first admin, policy, slots, profile facts, booking smoke path, notification, audit, and reporting evidence before live use. |

## Planned Slices

| Slice | Purpose | Notes |
| --- | --- | --- |
| `CUST002` SSO-First Customer Integration Contract | Define SSO mapping, minimal profile data, classification, local-account fallback, validation, and audit behavior. | This page is the source-of-truth contract for downstream implementation slices. |
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
