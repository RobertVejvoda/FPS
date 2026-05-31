# Architecture Review

Architecture review checks whether an architecture change is coherent, traceable, secure enough for its stated use, and ready to guide delivery or customer-facing claims.

## Review Triggers

- New architecture-significant requirement.
- New bounded context, integration, data store, or deployment profile.
- Security, privacy, compliance, or operational risk change.
- Exception to an approved principle or standard.
- Hosted pilot or customer production readiness claim.

## Checklist

- Stakeholder concern is identified.
- Baseline/current-state evidence is separated from target architecture.
- Alternatives and rationale are documented where the decision is durable.
- Security, privacy, and operational impact are considered.
- Gaps and work packages are linked.
- Decision is recorded if durable.

## Review Levels

| Level | When Used | Expected Evidence |
| --- | --- | --- |
| Local docs review | Clarifications, link fixes, migration cleanup, non-behavioral edits. | Markdown clarity, internal links, artifact metadata consistency. |
| Architecture review | New target statements, cross-service design, data/store changes, workflow/runtime decisions, deployment profile changes. | Affected artifacts, rationale, gaps, validation plan, decision record when durable. |
| Security/privacy review | Tenant isolation, auth, secrets, audit, GDPR, WAF, encryption, retention, or production operations changes. | Threat/risk consideration, controls, accepted gaps, owner for residual risk. |
| Customer-readiness review | Hosted demo, pilot, client-owned production, or external architecture package. | Approved scope, open gaps, smoke evidence, deployment profile, support/operation assumptions. |

## Validation Expectations

- Documentation-only changes require Markdown review, link checks where relevant, and consistency with artifact/register status.
- Code or infrastructure changes require the relevant validation command from the issue or repository guidance.
- UI/UX changes require build/typecheck evidence and visual notes or screenshots when user-visible.
- Architecture/spec changes that will drive implementation should include acceptance criteria and expected validation for implementers.

## Robert TODOs

- Robert TODO: confirm the minimum review level for publishing the customer-facing architecture repository.
- Robert TODO: confirm whether customer pilot review requires a separate customer-readiness checklist artifact.
