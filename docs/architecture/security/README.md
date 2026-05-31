# Security Architecture

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Target |
| **ADM Phase** | Cross-cutting |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | - |
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
