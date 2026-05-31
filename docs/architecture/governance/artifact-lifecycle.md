# Artifact Lifecycle

Architecture artifacts move through lightweight lifecycle states. The state is about whether the artifact can guide implementation or customer-facing architecture claims.

| Status | Meaning |
| --- | --- |
| Draft | Content is being prepared and should not be treated as accepted. |
| In Review | Content is ready for stakeholder or architecture review. |
| Approved | Accountable owner accepts the artifact for its stated scope. |
| Baselined | Artifact is part of a named architecture baseline or target version. |
| Deprecated | Artifact is retained for history but should not guide new work. |
| Superseded | Artifact has been replaced by another artifact or version. |

## Lifecycle Rules

- Draft artifacts may guide discussion but not implementation governance.
- Approved artifacts can guide implementation and review.
- Baselined artifacts define the active architecture version.
- Deprecated or superseded artifacts must link to their replacement where possible.
- Git history remains the detailed change log.

## Metadata Rules

- Governed artifacts should include the standard metadata table from [Artifact Register](/architecture/artifact-register).
- Metadata tables use blank headers, bold field names, and normal values.
- Version changes are page-level unless a named architecture version is being changed in [Architecture Version Register](/architecture/architecture-states/architecture-version-register).
- `Last Reviewed` records the last meaningful architecture review, not every typo fix.
- `Next Review` should name an event trigger when a calendar date is not useful.

## Lifecycle Gates

| Transition | Minimum Evidence |
| --- | --- |
| Draft -> In Review | Scope is clear, source evidence is linked, open gaps/TODOs are explicit, and affected stakeholders are identified. |
| In Review -> Approved | Accountable owner accepts scope, assumptions, open gaps, and known risks. |
| Approved -> Baselined | Artifact is tied to a named architecture version and required companion artifacts are also approved or explicitly deferred. |
| Any state -> Deprecated | Replacement or reason for retirement is recorded. |
| Any state -> Superseded | Replacement artifact/version is linked. |

## Robert TODOs

- Robert TODO: confirm whether approval evidence should be captured in PR reviews only or also copied into the artifact metadata/history.
