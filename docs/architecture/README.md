# Architecture Repository

This section is the new lightweight TOGAF 10-inspired architecture repository for FairSpot.

It is intentionally separate from the older layer-based documentation. Existing pages under `business-layer/`, `application-layer/`, `technology-layer/`, `security/`, `production/`, and the delivery trackers remain source evidence until their content is migrated or superseded. New architecture governance and target-state content should be added under `docs/architecture/`.

FairSpot primarily models target architecture. The baseline is current-state evidence: implemented product behavior, current documentation, deployment assumptions, and known gaps. Baseline evidence exists to support gap analysis; it is not treated as a complete enterprise baseline architecture.

## Repository Model

| Area | New Architecture Repository Page |
| --- | --- |
| TOGAF ADM map | [TOGAF ADM Map](/architecture/togaf-adm-map) |
| Lightweight ADM execution checklist | [ADM Checklist](/architecture/adm-checklist) |
| Artifact status and page versions | [Artifact Register](/architecture/artifact-register) |
| Legacy-to-repository migration status | [Architecture Migration Tracker](/architecture/migration-tracker) |
| Architecture vision | [Architecture Vision](/architecture/architecture-vision) |
| Stakeholders and concerns | [Stakeholders and Concerns](/architecture/stakeholders-and-concerns) |
| Constraints | [Constraints](/architecture/constraints) |
| Architecture decision log | [Architecture Decisions](/architecture/decisions) |
| Architecture vocabulary | [Architecture Glossary](/architecture/glossary) |
| Business architecture | [Business Architecture](/architecture/business/) |
| Information systems architecture | [Information Systems](/architecture/information-systems/) |
| Technology architecture | [Technology Architecture](/architecture/technology/) |
| Security architecture | [Security Architecture](/architecture/security/) |
| Governance | [Governance](/architecture/governance/) |
| Implementation and migration | [Implementation And Migration](/architecture/implementation-migration/) |
| Views and diagrams | [Views and Diagrams](/architecture/views/) |
| Baseline, target, transition, and gap tracking | [Architecture States](/architecture/architecture-states/) |
| Durable decisions | [Versions and Decisions](/versions-and-decisions) |
| Legacy/source evidence | [Legacy Architecture Evidence](/architecture-views) |

## Rules

- Keep new architecture content under `docs/architecture/`.
- Treat older layer pages as source evidence, not as the target architecture repository structure.
- Treat [Legacy Architecture Evidence](/architecture-views) as a bridge to old view hierarchy evidence, not as a current architecture entry point.
- Use Docsify absolute links (`/path/to/page`) inside nested pages so local and hosted routes behave the same.
- Treat page status and version as artifact metadata, not as a replacement for Git history.
- Mark assumptions and current-state evidence clearly when a complete baseline does not exist.
- Keep missing layers visible as placeholders instead of leaving readers to infer gaps from legacy documentation.
