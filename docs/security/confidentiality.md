## Confidentiality Overview

Confidentiality in FairSpot protects tenant, employee, booking, notification, audit, reporting, and operational data from unauthorized access. The concrete hosting provider does not change the classification rules.

### Application Components Confidentiality

| Component | Confidentiality Level | Notes |
| --- | --- | --- |
| Web application | Confidential | Handles authenticated employee, HR, admin, reporting, and audit views. |
| Mobile application | Confidential | Handles employee self-service data and access tokens. |
| Backend APIs | Confidential | Enforce tenant/user context and role checks. |
| Databases and state stores | Confidential | Store tenant-scoped operational data and read models. |
| Identity provider | Confidential | Owns authentication, sessions, and mapped claims. |
| Notification | Confidential | Stores user-visible notification records and delivery metadata. |
| Audit | Confidential | Stores pseudonymised business activity and restricted PII mappings. |
| Technical telemetry | Internal | Must not include secrets, tokens, or raw PII. |
| Secret store | Secret | Holds credentials, keys, tokens, certificates, and connection strings. |
| Object storage | Confidential | Holds tenant-owned documents, reports, exports, backup artifacts, and branding assets. |
| Third-party integrations | Internal or Confidential | Classification depends on data exchanged and client approval. |

### Provider-Managed Services

Provider-managed services inherit the classification of the data or credential they process. For example:

- a managed database that stores booking data is Confidential;
- a secret manager that stores connection strings is Secret;
- a monitoring platform that receives safe technical telemetry is Internal unless it receives Confidential data;
- an email/SMS provider becomes a subprocessor when enabled for customer data.

Release 1 uses NAS/Cloudflare for hosted evaluation, and the cloud-hosted follow-up target is DigitalOcean. Client-owned production may use the client's approved platform as long as the same confidentiality rules are enforced.
