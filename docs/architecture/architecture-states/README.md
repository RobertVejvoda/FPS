# Architecture States

Architecture state pages explain what exists now, what should exist, how FairSpot moves between states, and which gaps must be closed.

FairSpot uses lightweight current-state evidence as its baseline. The target architecture is the main architecture model.

## Architecture State Types

| State Type | Meaning | FairSpot Use |
| --- | --- | --- |
| Baseline | A named snapshot of current architecture or current-state evidence. | Implemented product state, current docs, current deployment assumptions, and known gaps. |
| Target | A named future architecture accepted or under review. | Customer-ready FairSpot architecture for hosted pilot and client evaluation. |
| Transition | An intermediate architecture state between baseline and target. | Milestone or slice groups that move FairSpot toward deployability. |
| Gap Analysis | Structured comparison between baseline and target. | Customer-first readiness gaps and architecture implementation gaps. |
| Risk | Uncertain condition that can affect customer-ready target outcomes. | Delivery, integration, operations, data, and mobile risks with owners and review dates. |

## Versioning Rules

- Use named architecture versions such as `Current State v0.1` and `Customer-Ready Target v0.1`.
- Use page-level versions such as `0.1`, `0.2`, and `1.0`.
- `1.0` means the artifact is first approved or baselined for its stated scope.
- Git history remains the detailed change log.
- Repository tags may be used later for formal architecture baselines.

## Contents

- [Baseline Architecture](/architecture/architecture-states/baseline-architecture)
- [Target Architecture](/architecture/architecture-states/target-architecture)
- [Transition Architectures](/architecture/architecture-states/transition-architectures)
- [Architecture Version Register](/architecture/architecture-states/architecture-version-register)
- [Gap Analysis](/architecture/architecture-states/gap-analysis)
- [Risk Register](/architecture/architecture-states/risk-register)
