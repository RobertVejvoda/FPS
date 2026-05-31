# Security Architecture

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.4 |
| **Architecture State** | Target |
| **ADM Phase** | Cross-cutting |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-05-31 |
| **Next Review** | Before hosted pilot |

Security architecture describes FairSpot identity, tenant isolation, privacy, controls, and known gaps.

## Migration Status

Core security and privacy direction has been restated from the legacy security model and hosted-pilot hardening docs. It remains `Draft` because hosted WAF/auth validation, Dapr production hardening, retention jobs, Customer/DataHub persistence, and privacy workflow evidence are not complete.

| Area | Status | Notes |
| --- | --- | --- |
| Security architecture | Partial | Identity, tenant isolation, Dapr, ingress, secrets, audit, and observability boundaries are stated. |
| Privacy architecture | Partial | Data classes, minimization, audit pseudonymisation, erasure, and retention concerns are stated. |
| Controls | Partial | Architecture-significant controls are stated with evidence links. |
| Gap register | Partial | High-impact hosted pilot and production-blocking gaps are visible. |

## Requirement Interpretation

| Requirement | Security / Privacy Interpretation | Evidence | Gap |
| --- | --- | --- | --- |
| AR-001 | Tenant isolation is enforced across authorization, storage, events, DataHub projections, audit, backup, and support flows. | [Security Architecture](/architecture/security/security-architecture), [Controls](/architecture/security/controls) | Tenant-isolation evidence across DataHub and hosted profile. |
| AR-003 / AR-014 | Public ingress must be WAF-protected and must not expose internal/admin/debug/runtime/observability surfaces. | [Security Architecture](/architecture/security/security-architecture), [Controls](/architecture/security/controls), [Gap Register](/architecture/security/gap-register) | Hosted WAF smoke and origin exposure evidence. |
| AR-004 / AR-008 | Employee and HR views expose only role-appropriate safe explanations and require audit for privileged cancellation/support actions. | [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls) | HR cancellation audit/notification validation. |
| AR-009 / AR-013 | Dapr and DataHub must use least-privilege components, secret scopes, tenant-scoped projections, minimal payloads, and approved export paths. | [Controls](/architecture/security/controls), [Data Architecture](/architecture/information-systems/data-architecture) | Dapr hardening and DataHub privacy-shape proof. |
| AR-017 / AR-020 | Resource-map publication and impact preview must preserve tenant scope, audit publication actions, and avoid exposing employee-level or hidden allocation detail. | [Security Architecture](/architecture/security/security-architecture), [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls) | Role-specific UI/API and projection privacy evidence. |
| AR-019 | Sponsor summaries must be aggregate by default and must not expose employee-level detail unless a separate authorized HR/audit purpose exists. | [Privacy Architecture](/architecture/security/privacy-architecture), [Controls](/architecture/security/controls) | Sponsor-safe projection/export validation. |
| AR-021 / AR-022 | Backup/restore, Dapr components, sidecars, state encryption, resiliency, secret references, and component scopes require hosted-profile evidence before customer traffic. | [Controls](/architecture/security/controls), [Gap Register](/architecture/security/gap-register), [Runtime Platform](/architecture/technology/runtime-platform) | Restore drill and Dapr hardening proof. |

## Legacy Evidence Disposition

| Legacy Source | Target Disposition |
| --- | --- |
| `security/**` | Source evidence for controls, threat assumptions, data classification, and review pack. Target controls and gaps belong here. |
| `security/cloudflare-waf-profile.md` | Migrated directionally into Security and Technology; keep as executable WAF profile evidence. |
| `security/gap-register.md` | Migrated directionally into [Security Gap Register](/architecture/security/gap-register); keep until all rows are mapped or closed. |
| GDPR/security user stories | Source evidence for privacy architecture and controls; not all are customer-ready P0 implementation slices. |

## Contents

- [Security Architecture](/architecture/security/security-architecture)
- [Privacy Architecture](/architecture/security/privacy-architecture)
- [Controls](/architecture/security/controls)
- [Gap Register](/architecture/security/gap-register)

## Source Evidence

- [Security Model](/security/security-model)
- [Data Privacy](/security/data-privacy)
- [Cloudflare WAF Profile](/security/cloudflare-waf-profile)
- [Security Gap Register](/security/gap-register)
- [Dapr-First Production Standards](/production/dapr-first-production-standards)
