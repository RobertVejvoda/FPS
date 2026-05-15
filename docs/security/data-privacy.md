Ensures that sensitive data is handled in compliance with privacy regulations and best practices, such as data minimization and anonymization.
## Purpose

Data privacy defines how FPS limits, protects, retains, and exposes personal and tenant data. It complements the [Security Model](./security-model) by translating the data classification and GDPR alignment into implementation and operational rules.

## Personal and Tenant Data

FPS commonly processes these Confidential data categories:

- employee profile identifiers, vehicle facts, accessibility or company-car eligibility, and parking preferences;
- booking requests, allocation outcomes, cancellations, no-show signals, penalties, and employee-visible reasons;
- notifications and delivery metadata;
- tenant locations, parking spaces, policy configuration, role assignments, and operational reports;
- audit records, support cases, and security investigation evidence;
- billing contacts, invoices, subscriptions, and usage records where billing is enabled.

Secret data, such as credentials, signing keys, tokens, certificates, connection strings, and recovery material, is not normal personal data access. It is governed by secret-management controls and separate access tracking.

## Privacy Rules

- Collect only data required for parking operations, auditability, notification, reporting, billing, or legally required administration.
- Resolve tenant and user context from authenticated claims or trusted service context, never from caller-supplied identity fields.
- Default employee views to own data only.
- Do not expose hidden lottery weights, seeds, internal diagnostic details, unrelated employee data, or raw audit internals to employees.
- Pseudonymise audit actors where possible so immutable evidence can remain useful after PII mappings are removed.
- Keep events minimal: no secrets, stack traces, raw names, emails, license plates, or unrelated employee details unless a documented consumer requires them.
- Define retention before production for bookings, notifications, audit records, reports, logs, backups, support cases, and PII mappings.

## GDPR Alignment

FPS supports GDPR-aligned operation through tenant scoping, least privilege, data minimisation, audit evidence, encryption in transit and at rest, and rights-request slices for access, rectification, erasure, and restriction. Product documentation does not certify GDPR compliance by itself; production use still requires controller/processor roles, privacy notices, subprocessors, data-processing agreements, retention schedules, and legal review.

## Rights Requests

Access, rectification, and erasure workflows must identify all service-owned data for the affected tenant/user. Immutable audit evidence should preserve accountability while removing or disconnecting direct PII mappings where legally allowed.
