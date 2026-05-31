# Architecture Repository

This section applies the lightweight TOGAF 10-inspired architecture repository pattern to FairSpot without moving the existing documentation set.

FairSpot primarily models target architecture. The baseline is current-state evidence: implemented product behavior, current documentation, deployment assumptions, and known gaps. Baseline evidence exists to support gap analysis; it is not treated as a complete enterprise baseline architecture.

## Repository Model

| Area | FairSpot Source |
| --- | --- |
| Architecture summary and viewpoints | [Architecture Views](/architecture-views) |
| TOGAF ADM map | [TOGAF ADM Map](/architecture/togaf-adm-map) |
| Artifact status and page versions | [Artifact Register](/architecture/artifact-register) |
| Baseline, target, transition, and gap tracking | [Architecture States](/architecture/architecture-states/) |
| Durable decisions | [Versions and Decisions](/versions-and-decisions) |

## Rules

- Keep existing layer pages as the content source until a later restructure is approved.
- Use this section for governance, architecture state, versioning, and gap traceability.
- Use Docsify absolute links (`/path/to/page`) inside nested pages so local and hosted routes behave the same.
- Treat page status and version as artifact metadata, not as a replacement for Git history.
- Mark assumptions and current-state evidence clearly when a complete baseline does not exist.
