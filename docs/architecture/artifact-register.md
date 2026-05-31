# Architecture Artifact Register

This register tracks the status, version, ownership, and review state of FairSpot architecture artifacts.

## Artifact Metadata Standard

Major architecture pages should use this header when they become governed artifacts.

| Field | Value |
| --- | --- |
| Status | Draft / In Review / Approved / Baselined / Deprecated / Superseded |
| Version | 0.1 |
| Architecture State | Baseline / Target / Transition / Gap Analysis / Cross-cutting |
| Baseline Version | Current State v0.1 |
| Target Version | Customer-Ready Target v0.1 |
| ADM Phase | Preliminary / A / B / C / D / E / F / G / H / Requirements Management |
| Responsible | Architecture Owner |
| Accountable | Robert / Architecture Board |
| Last Reviewed | YYYY-MM-DD |
| Next Review | YYYY-MM-DD or event-triggered |

## Register

| Artifact | Path | ADM Phase | Architecture State | Status | Version | Responsible | Accountable | Last Reviewed | Next Review |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Architecture Views | [Architecture Views](/architecture-views) | Cross-ADM | Target | Draft | 0.1 | Codex/Product Owner | Robert | - | On major architecture change |
| TOGAF ADM Map | [TOGAF ADM Map](/architecture/togaf-adm-map) | Preliminary | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | - | On structure change |
| Business Architecture | [Functional Architecture](/business-layer/functional-architecture) | Phase B | Target | Draft | 0.1 | Codex/Product Owner | Robert | - | Before client architecture review |
| Information Systems Architecture | [Software Architecture](/technology-layer/software-architecture) | Phase C | Target | Draft | 0.1 | Codex/Product Owner | Robert | - | Before client architecture review |
| Technology Architecture | [Technology Layer](/technology-layer) | Phase D | Target | Draft | 0.1 | Codex/Product Owner | Robert | - | Before hosted pilot |
| Security Architecture | [Security Model](/security/security-model) | Cross-cutting | Target | Draft | 0.1 | Codex/Product Owner | Robert | - | Before hosted pilot |
| Architecture States | [Architecture States](/architecture/architecture-states/) | Cross-ADM | Baseline / Target / Gap Analysis | Draft | 0.1 | Codex/Product Owner | Robert | - | On milestone change |

## Status Definitions

| Status | Meaning |
| --- | --- |
| Draft | Content is being prepared and should not be treated as accepted. |
| In Review | Content is ready for stakeholder or architecture review. |
| Approved | Accountable owner accepts the artifact for its stated scope. |
| Baselined | Artifact is part of a named architecture baseline or target version. |
| Deprecated | Artifact is retained for history but should not guide new work. |
| Superseded | Artifact has been replaced by another artifact or version. |
