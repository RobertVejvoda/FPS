# Customer Data Import and Integration

FPS needs a controlled way to populate employee profile facts used by Booking: employee identity, display name, email, active status, roles, vehicle facts, company-car eligibility, accessibility needs, and location assignment. This page records the planned integration model and data classification so future implementation does not accidentally treat HR/customer data as ordinary configuration.

## Integration Goal

Customer data import should answer three questions:

| Question | Expected answer |
| --- | --- |
| Who is allowed to use FPS? | Active employee identity and tenant membership come from the customer's identity or HR source. |
| What profile facts affect parking eligibility? | Vehicle, company-car, accessibility, location, and policy-related eligibility facts are imported or managed by authorized roles. |
| Which system owns the truth? | FPS stores only the facts it needs for booking, notification, audit, reporting, and support; the customer system remains the source of truth unless explicitly configured otherwise. |

## Candidate Integration Modes

| Mode | Use When | Data Shape | Notes |
| --- | --- | --- | --- |
| Manual admin entry | Small demo, small tenant, or early pilot. | Tenant admin creates users/profile facts in FPS UI. | Lowest integration cost, but not scalable. |
| CSV import | First production migration or periodic low-volume sync. | Validated CSV with tenant, employee ID, name, email, status, location, vehicle facts, and eligibility flags. | Good first implementation because it is explicit, reviewable, and easy to test. |
| HR system export | Customer has Workday, SAP SuccessFactors, BambooHR, Personio, or similar. | Scheduled file or API pull mapped to FPS profile schema. | Needs customer-specific field mapping and data-processing review. |
| Identity provider claims | Customer IdP can provide stable tenant/user/role/location claims. | OIDC/SAML/SCIM-derived user identity and role/location facts. | Good for identity and roles; vehicle data usually still needs Profile/HR source. |
| SCIM provisioning | Customer supports SCIM user lifecycle. | User create/update/deactivate, group/role assignment. | Useful for active status and role lifecycle, not usually enough for vehicle facts. |
| Custom API integration | Enterprise client requires near-real-time sync. | Client-specific contract behind an integration actor. | Later option; requires scoped credentials, retries, idempotency, monitoring, and audit. |

## Minimum Data Contract

| Field | Required | Classification | Source of truth | Notes |
| --- | --- | --- | --- | --- |
| `tenantId` | Yes | Confidential | FPS/customer tenant setup | Must come from trusted context or import metadata, never arbitrary user input. |
| `employeeId` | Yes | Confidential | Customer HR/IdP | Stable external employee key; not necessarily the same as FPS `userId`. |
| `userId` | Yes after onboarding | Confidential | FPS Identity | Internal FPS user key mapped to authenticated claims. |
| `displayName` | Optional for v1 UI | Confidential | Customer HR/IdP | Avoid in events/audit where ID is enough. |
| `email` | Required for email notification | Confidential | Customer HR/IdP | Used by Notification; avoid in audit/event payloads unless required. |
| `activeStatus` | Yes | Confidential | Customer HR/IdP | Inactive users cannot create new parking requests. |
| `roles` | Yes for admin/HR/auditor | Confidential | Customer IdP/admin | Role assignment must be auditable. |
| `homeLocationId` | Optional | Confidential | Customer HR/admin | Used for default location and reporting filters. |
| `vehicles` | Policy-dependent | Confidential | Employee/Profile/HR | Includes license plate or operational vehicle identifier when required. |
| `hasCompanyCar` | Policy-dependent | Confidential | HR/fleet system | Affects Tier 1 allocation; changes must be auditable. |
| `accessibilityNeeds` | Policy-dependent | Confidential, sensitive | HR/authorized admin | Use minimum required flags, not medical detail. |
| `notificationPreferences` | Optional | Confidential | Employee/Profile | Mandatory operational notifications remain non-disableable. |

## Security and Privacy Rules

- Imported employee/profile data is **Confidential** unless it contains credentials, tokens, or integration keys, which are **Secret**.
- Integration credentials are Secret and must be stored in the selected secret-management system, never in CSV files, GitHub issues, PRs, logs, or screenshots.
- Import files must be encrypted at rest and deleted or retained according to customer-approved retention policy.
- Import previews and validation errors must mask Confidential fields where possible.
- Every import must be tenant-scoped, idempotent, and traceable to an actor or integration identity.
- Role, company-car, accessibility, active/inactive status, and vehicle eligibility changes must be auditable because they can affect allocation outcomes.
- Audit records should store stable IDs and reasons, not raw names, emails, or license plates unless explicitly required by a documented audit use case.
- GDPR rights requests must include imported profile facts, mappings from external employee IDs to FPS users, and any retained import files.

## Validation Rules

| Rule | Reason |
| --- | --- |
| Reject rows without tenant, employee ID, and active/inactive status. | Prevent ambiguous identity and lifecycle state. |
| Validate role names against FPS role catalog. | Prevent accidental privilege creation. |
| Validate location and policy references against Configuration. | Prevent orphaned profile facts. |
| Validate vehicle fields only when policy requires vehicles. | Avoid collecting unnecessary data. |
| Detect duplicate employee IDs per tenant. | Keep external mapping stable. |
| Require reason and actor for manual corrections to imported eligibility facts. | Preserve fairness and auditability. |
| Produce an import summary before commit. | Let HR/admin review creates, updates, deactivations, and rejects. |

## Planned Slices

| Slice | Purpose | Notes |
| --- | --- | --- |
| `CUST002` Customer Data Import Contract | Define import schema, validation, classification, and preview/commit behavior. | Documentation/spec slice before implementation. |
| `P002` Profile Import And Mapping | Implement CSV or file-based import into Profile with tenant/user mapping and validation summary. | First implementation path; should not require HR API integration. |
| `ID002` User Provisioning Integration | Map imported users to Identity/claims/roles and deactivation behavior. | Could use IdP claims or SCIM later. |
| `OPS005` Integration Secrets And Observability | Define secret handling, import logs, retry/error evidence, and metrics for integration actors. | Needed before customer-owned production integration. |

## Open Questions

- Which customer system is likely first: manual CSV, HR export, IdP claims, or SCIM?
- Is license plate required for v1 parking policy, or can vehicles be identified by type/capability only?
- Who is allowed to edit company-car and accessibility eligibility after import?
- Should employees self-maintain vehicles, or should HR/fleet data be authoritative?
- What import-file retention period is acceptable for demo and production?
