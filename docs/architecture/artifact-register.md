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
| TOGAF ADM Map | [TOGAF ADM Map](/architecture/togaf-adm-map) | Preliminary | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | - | On structure change |
| Architecture Migration Tracker | [Architecture Migration Tracker](/architecture/migration-tracker) | Cross-ADM | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | - | During each migration slice |
| Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Phase A | Target | Draft | 0.1 | Codex/Product Owner | Robert | - | Before client architecture review |
| Business Architecture | [Business Architecture](/architecture/business/) | Phase B | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Information Systems Architecture | [Information Systems](/architecture/information-systems/) | Phase C | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before client architecture review |
| Technology Architecture | [Technology Architecture](/architecture/technology/) | Phase D | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before hosted pilot |
| Security Architecture | [Security Architecture](/architecture/security/) | Cross-cutting | Target | Draft | 0.2 | Codex/Product Owner | Robert | 2026-05-31 | Before hosted pilot |
| Governance | [Governance](/architecture/governance/) | Preliminary + G/H | Cross-cutting | Draft | 0.1 | Codex/Product Owner | Robert | - | On governance change |
| Views and Diagrams | [Views and Diagrams](/architecture/views/) | Cross-ADM | Target | Draft | 0.1 | Codex/Product Owner | Robert | - | On diagram/model change |
| Architecture States | [Architecture States](/architecture/architecture-states/) | Cross-ADM | Baseline / Target / Gap Analysis | Draft | 0.1 | Codex/Product Owner | Robert | - | On milestone change |

## Layer Completeness

The architecture repository must show all expected layers even when content is incomplete.

| Layer / Area | Required Artifact | Completeness Status |
| --- | --- | --- |
| Preliminary and governance | [Governance](/architecture/governance/) | Partial |
| Phase A - Architecture Vision | [Architecture Vision](/architecture/architecture-vision) | Placeholder |
| Requirements Management | [Requirements](/architecture/requirements) | Placeholder |
| Phase B - Business Architecture | [Business Architecture](/architecture/business/) | Partial |
| Phase C - Information Systems Architecture | [Information Systems](/architecture/information-systems/) | Partial |
| Phase D - Technology Architecture | [Technology Architecture](/architecture/technology/) | Partial |
| Cross-cutting Security Architecture | [Security Architecture](/architecture/security/) | Partial |
| Architecture states and gaps | [Architecture States](/architecture/architecture-states/) | Partial |
| Views and diagrams | [Views and Diagrams](/architecture/views/) | Partial |
| Deferred billing scope | [Architecture Migration Tracker](/architecture/migration-tracker) | Deferred |
| Obsolete reporting direction | [Architecture Migration Tracker](/architecture/migration-tracker) | Deferred |

## Status Definitions

| Status | Meaning |
| --- | --- |
| Draft | Content is being prepared and should not be treated as accepted. |
| In Review | Content is ready for stakeholder or architecture review. |
| Approved | Accountable owner accepts the artifact for its stated scope. |
| Baselined | Artifact is part of a named architecture baseline or target version. |
| Deprecated | Artifact is retained for history but should not guide new work. |
| Superseded | Artifact has been replaced by another artifact or version. |
