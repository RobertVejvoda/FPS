---
title: Security Model
---

## Purpose

This page defines the FPS security model across actors, roles, data classification, transport/storage protections, secret handling, and GDPR-aligned privacy controls.

It is an architecture and product control document, not a legal certification. Final GDPR compliance still depends on the operating company, deployment region, retention schedules, subprocessors, and legal review.

## Security Principles

- Identity, tenant, user, and role context come from authenticated claims or trusted system context only.
- Authorization is tenant-scoped and least-privilege by default.
- Confidential and Secret data must not be exposed in logs, events, GitHub issues, pull requests, telemetry labels, or employee-visible error messages.
- Services fail closed when required identity, tenant, or role claims are missing.
- Human access to production data is exceptional, justified, approved, time-bound, and audited.
- Audit evidence is append-only where possible and stores pseudonymised actors instead of raw names or emails.

## Actors and Responsibilities

| Actor | Primary responsibility | Security responsibility |
| --- | --- | --- |
| Employee | Requests, views, confirms, and cancels their own parking reservations. | Protects their account, sees only their own booking/profile data, and never receives other employees' confidential allocation details. |
| HR or Facility Manager | Operates parking policy, exceptions, manual corrections, and operational reporting. | Uses tenant/location-scoped access, records reasons for manual actions, and protects confidential employee and allocation data. |
| Customer Administrator | Manages tenant configuration, users, roles, locations, spaces, and policy setup. | Grants roles according to customer policy, reviews privileged access, and avoids using admin access for routine employee actions. |
| Auditor | Reviews policy-sensitive business events, manual overrides, access history, and compliance evidence. | Uses read-only audit access, protects audit exports, and escalates suspicious activity. |
| Finance or Accounting User | Manages billing, invoices, subscriptions, and cost-recovery records where enabled. | Sees only billing-relevant tenant and usage data, not detailed employee booking history unless explicitly authorized. |
| IT Support Operator | Diagnoses operational issues and availability incidents. | Uses minimal metadata access, avoids routine production data access, and records break-glass or customer-support reasons. |
| Security Officer or Data Protection Contact | Handles security incidents, DPIA inputs, GDPR requests, and privileged investigations. | Approves or reviews exceptional access to Confidential or Secret data and validates retention, erasure, and breach evidence. |
| System Actor | Runs scheduled jobs, retries, projections, notification delivery, and service workflows. | Uses service identity, least-privilege credentials, idempotent operations, and emits traceable system audit events. |
| Integration Actor | Calls FPS through documented machine-to-machine integrations. | Uses scoped credentials, rotates secrets, and is limited to explicit tenant and API permissions. |
| Developer or Operator | Builds, deploys, and maintains FPS. | Has no routine customer-data access; production access is through audited deployment, monitoring, and break-glass procedures. |

## Role to Data Access

| Role | Public | Internal | Confidential | Secret |
| --- | --- | --- | --- | --- |
| Employee | Yes | No | Own profile, own booking requests, own allocation status, own notifications. | No. |
| HR or Facility Manager | Yes | Limited operational documentation. | Tenant/location employee booking, eligibility, exception, and operational report data needed for parking operations. | No. |
| Customer Administrator | Yes | Tenant administration documentation. | Tenant users, roles, locations, spaces, policies, and configuration history. | No direct read access; tenant-managed secrets must use write-only or masked interfaces. |
| Auditor | Yes | Audit process documentation. | Read-only tenant audit records and pseudonymised traces; raw PII mapping only when separately approved. | No. |
| Finance or Accounting User | Yes | Billing process documentation. | Billing, invoice, subscription, and cost-recovery data; detailed employee booking data only when billing policy requires it. | No. |
| IT Support Operator | Yes | Operational runbooks and non-sensitive telemetry. | Minimal metadata required for a support case or incident, time-bound and audited. | No routine access. Break-glass only with approval. |
| Security Officer or Data Protection Contact | Yes | Security and privacy procedures. | Confidential investigation data, audit exports, PII mappings, and GDPR request evidence when justified. | Time-bound access only for key rotation, incident response, or recovery. |
| System Actor | No human access. | Runtime configuration needed by the service. | Processes tenant data required by its service contract. | Runtime-only credential access through secret store or environment injection. |
| Developer or Operator | Yes | Source, architecture, CI/CD, observability metadata. | No routine access; masked/sampled data only unless a production incident requires approved access. | No routine access; audited break-glass for deployment/recovery secrets only. |

## Data Classification

| Classification | Definition | FPS examples | Baseline controls |
| --- | --- | --- | --- |
| Public | Approved for public release. | Open-source README, public docs, license, architecture overview without tenant data, public CI status. | Normal integrity review before publishing. No customer or secret content. |
| Internal | Useful to contributors/operators but not sensitive customer data. | Development plan, non-sensitive runbooks, deployment topology, aggregate service health, non-customer test data. | Repository access control, review before sharing outside maintainers, no tenant identifiers unless already public. |
| Confidential | Customer, employee, operational, or audit data that could affect privacy, fairness, security, or business operations. | Tenant ID, user ID, roles, employee profile, license plate, booking requests, allocation outcomes, penalties, notifications, support cases, audit records, PII mapping, policy configuration, reporting exports. | Authenticated and authorized access, tenant scoping, encryption in transit and at rest, masking in logs, audit for administrative and sensitive reads/writes. |
| Secret | Credentials or cryptographic material that can grant access, decrypt data, impersonate users/services, or alter trust boundaries. | Access/refresh tokens, signing keys, OAuth client secrets, API keys, database connection strings, Vault secrets, GitHub tokens, private certificates, backup encryption keys, recovery credentials. | Secret manager storage, no plaintext logs or repository storage, rotation, short-lived credentials where possible, dual-control or approval for break-glass, access audit. |

## Data Layer

FPS stores tenant data in service-owned MongoDB databases with tenant-specific collections. Collection names are derived centrally from authenticated or trusted service context using sanitised tenant keys. Request bodies, query strings, headers from untrusted callers, and user-supplied collection names must not choose the tenant collection.

Data in transit:

| Flow | Data examples | Protection |
| --- | --- | --- |
| Mobile/web client to API gateway | OIDC authorization responses, JWT access tokens, booking commands, profile/session reads, notification reads. | HTTPS with TLS 1.2+; OIDC Authorization Code + PKCE for mobile; no client secret in mobile app. |
| API gateway to backend service | Authenticated HTTP requests and JWT claims. | HTTPS or trusted internal transport; services validate tokens and required claims. |
| Service-to-service command query | Profile snapshots, configuration reads, booking state queries. | Dapr service invocation or HTTPS; Dapr mTLS/Sentry is the baseline for service identity. |
| Domain events | Booking events for Notification, Audit, and Reporting. | Dapr pub/sub over RabbitMQ; events omit secrets, stack traces, hidden lottery internals, and unrelated employee data. |
| Notification delivery | In-app notifications, future email/push messages. | Authenticated service delivery; external channels must use TLS and provider-scoped credentials. |
| Observability | Logs, metrics, traces, correlation IDs. | No secrets or raw confidential payloads in labels/log lines; access restricted to operators. |

Data at rest:

| Store | Data examples | Protection |
| --- | --- | --- |
| MongoDB write/read stores | Booking aggregates, profile snapshots, notification records, audit records, policy/configuration, reporting read models. | Encryption at rest, authenticated database access, tenant-specific collections, collection-scope backup/restore controls. |
| Dapr state store | Aggregate state and workflow state where used. | Same tenant-isolation rules as MongoDB; component strategy must not mix tenant data without explicit scoped keys. |
| Mobile SecureStore | OIDC access/refresh token material where supported. | Native secure storage; logout clears token state and in-memory session state. |
| Mobile AsyncStorage | Development bearer-token handoff only. | Development mode only; must not hold production credentials. |
| Object storage/backups | Future exports, reports, backup archives. | Encryption at rest, tenant-scoped paths, retention policy, restricted download access. |
| Vault/GitHub secrets/CI | Deployment credentials, API keys, certificates, signing material. | Secret store or CI secret facility; masked logs; scoped tokens; rotation and audit. |
| Logs/traces/metrics | Operational telemetry and security events. | Redaction, retention limits, restricted access, and correlation IDs instead of raw private payloads. |

## Protocols and Encryption

| Boundary | Protocol | Encryption/authentication requirement |
| --- | --- | --- |
| End user to FPS | HTTPS; OIDC Authorization Code + PKCE for mobile/web login. | TLS 1.2+; JWT validation by services; mobile app stores no client secret. |
| API gateway routing | HTTP(S) behind Traefik. | TLS at the edge; internal service endpoints remain protected and tenant-aware. |
| Service invocation | Dapr service invocation or HTTPS. | Dapr mTLS/Sentry for service identity; propagate user context only when the downstream service needs user-scoped authorization. |
| Pub/sub | Dapr pub/sub over RabbitMQ/AMQP. | TLS and authenticated broker access in production; event contracts exclude Secret data. |
| State persistence | Dapr state store and MongoDB driver. | Authenticated database connections, TLS in production, encryption at rest, and tenant-specific collections. |
| Cache | Redis. | Authenticated access, TLS in production when network boundary requires it, no Secret data unless explicitly approved and expiring. |
| Object storage | MinIO/S3-compatible API. | TLS, bucket/path authorization, encryption at rest, and tenant-scoped object naming. |
| Secret retrieval | Vault and CI secret mechanisms. | Authenticated, auditable access; secrets injected at runtime, not committed to source. |

## Secret Access Tracking

Secret access must be tracked separately from normal business audit because a Secret can expand an actor's authority.

Required records for human or automation access to Secret data:

- actor identity or service identity;
- role and approval source;
- reason or incident/ticket reference;
- secret path, logical key, or affected system without recording the secret value;
- action type such as read, write, rotate, revoke, export, or break-glass;
- timestamp, source environment, and correlation ID;
- expiry or follow-up action when access is temporary.

Rules:

- Secrets must never be printed in logs, issue comments, PRs, traces, crash reports, or test snapshots.
- Break-glass access requires a reason, an approver where operationally possible, and post-incident review.
- Secrets exposed to a human, CI log, third-party system, or untrusted environment must be rotated.
- Production secret reads and rotations should feed security monitoring so unusual access patterns are visible.
- Application audit should separately record sensitive administrative actions, PII-mapping access, report export/download, role assignment, OAuth/key rotation, and tenant configuration changes.

## GDPR Alignment

FPS aligns its security model with GDPR principles by design, but product documentation cannot by itself certify compliance.

| GDPR-aligned concern | FPS control |
| --- | --- |
| Lawfulness, fairness, transparency | Customer policy and tenant configuration must define why parking, profile, notification, billing, and audit data are processed. Employee-visible screens should explain relevant booking outcomes without exposing hidden allocation internals. |
| Purpose limitation | Booking data is used for parking operations, audit, notifications, reporting, and billing where enabled; new purposes require documentation and review. |
| Data minimisation | Events and audit records omit raw names, emails, secrets, stack traces, hidden lottery weights, and unrelated employee details. |
| Accuracy | Profile and configuration data are source inputs for allocation; authorized roles own correction workflows. |
| Storage limitation | Retention periods for bookings, notifications, audit records, reports, logs, backups, and PII mappings must be defined before production. |
| Integrity and confidentiality | Tenant scoping, RBAC/ABAC, encryption in transit and at rest, secret management, logging controls, and least privilege protect personal data. |
| Accountability | Manual actions require reasons where policy-sensitive; audit records, security logs, access reviews, and deployment evidence support later review. |
| Data protection by design/default | Employee views are own-data by default, audit actors are pseudonymised, and tenant collection resolution is centralised. |
| Rights requests | Access, rectification, erasure, and restriction workflows must be implemented as planned GDPR slices; audit pseudonymisation supports erasure by deleting PII mapping while preserving immutable anonymous evidence. |
| Breach response | Incident response must include detection, containment, impact analysis, affected data classes, regulator/customer notification assessment, and credential rotation where secrets are involved. |

Before production, FPS still needs explicit retention schedules, subprocessors and data-processing agreements, a record of processing activities, DPIA assessment where required, environment-specific security review, and customer-facing privacy notices.
