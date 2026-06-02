# Risk Register

This register tracks architecture-significant risks that are not only security gaps. Security-specific items remain in [Security Gap Register](/architecture/security/gap-register).

## Risks

| Risk ID | Area | Risk | Probability | Impact | Mitigation | Linked Artifact | Owner | Review Date | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RISK-001 | Delivery | Architecture artifacts may grow too broad to maintain as FairSpot expands beyond parking. | Medium | Medium | Keep root pages concise, use child pages for detail, and track status through Artifact Register. | [Artifact Register](/architecture/artifact-register) | Codex/Product Owner | 2026-06-15 | Open |
| RISK-002 | Integration | API and event contracts can drift from implementation if generated/source evidence is not linked. | Medium | High | Link generated API clients, OpenAPI contracts, event ownership, and compatibility expectations from Information Systems Architecture. | [API Contracts](/architecture/information-systems/api-contracts), [Integrations and Events](/architecture/information-systems/integrations-events) | Codex / Claude | 2026-06-15 | Open |
| RISK-003 | Operations | Restore process may not be proven before pilot launch. | Medium | High | Use backup/restore runbook evidence and require hosted smoke/restoration proof before customer data. | [Backup And Restore](/production/backup-restore), [Readiness](/architecture/implementation-migration/readiness) | Robert / Codex | Before hosted pilot | Open |
| RISK-004 | Operations | NAS/Cloudflare hosted profile may not be validated under realistic public-domain traffic. | Medium | High | Run hosted smoke, WAF, auth, no-internal-exposure, logging, and reset checks before pilot. | [Deployment Profiles](/architecture/technology/deployment-profiles), [Hosted Smoke Runbook](/production/hosted-smoke-runbook) | Robert / Codex | Before hosted pilot | Open |
| RISK-005 | Data | DataHub projections are incomplete, so booking outcome summaries and tenant readiness views may be unavailable. | High | Medium | Implement DataHub projection catalog, event inbox, first projections, and projection health. | [Data Architecture](/architecture/information-systems/data-architecture), [Gap Analysis](/architecture/architecture-states/gap-analysis) | Claude / Codex | During DataHub slices | Open |
| RISK-006 | Mobile | App-store build and real device-testing pipeline are not proven for pilot distribution. | Medium | Medium | Validate Expo build/device testing, auth, API environment config, and release notes before external mobile pilot. | [Mobile Device Testing](/production/mobile-device-testing), [Readiness](/architecture/implementation-migration/readiness) | Codex / Claude | Before external mobile pilot | Open |

## Risk Handling

| Status | Meaning |
| --- | --- |
| Open | Risk is known and needs mitigation, evidence, or acceptance. |
| Mitigating | Mitigation work is active or linked to a work package. |
| Accepted | Residual risk is accepted by Robert for the stated scope. |
| Closed | Evidence shows the risk is no longer material for the current target. |
