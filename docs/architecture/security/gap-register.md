# Security Gap Register

This page summarizes architecture-significant security gaps. Detailed security gaps remain in the legacy [Security Gap Register](/security/gap-register) until migrated.

| Gap | Impact | Mitigation | Owner | Status |
| --- | --- | --- | --- | --- |
| Hosted pilot end-to-end validation incomplete | Public deployment may expose unexpected runtime/auth/WAF issues. | Run hosted smoke, WAF, auth, and internal-path validation before public demo. | Codex/Robert | Open |
| Customer durable tenant state incomplete | Tenant setup may not survive restart where in-memory repositories remain. | Implement Customer durable state and validate restart behavior. | Claude/Codex | Open |
| DataHub projection privacy shape incomplete | Reports/read models may expose too much or too little detail. | Define first projection catalog and role-safe output shapes. | Codex/Claude | Open |
| Dapr production hardening incomplete | Service-to-service traffic, component access, state encryption, and resiliency behavior may be weaker than the target runtime boundary. | Validate Dapr mTLS/Sentry, component scopes, secret scopes, API tokens, resiliency policies, and state encryption for the hosted profile. | Codex/Claude | Open |
| Booking and notification retention jobs incomplete | Personal data may remain longer than customer/legal policy allows. | Define retention schedules and implement/smoke scheduled retention jobs before production processing of personal data. | Codex/Claude | Open |
| DataHub projection health incomplete | Operators may not detect stale projections, failed events, or duplicated processing before reports become misleading. | Implement event inbox status, projection lag, failed/poison event visibility, and rebuild state. | Codex/Claude | Open |
| Technical telemetry retention and access not validated | Logs/traces/metrics may expose data or be unavailable during incident review. | Define hosted telemetry retention, access control, redaction checks, and operator-only dashboard protection. | Codex/Robert | Open |
| Backup/restore security evidence incomplete | Restore operations may expose tenant, employee, audit, PII mapping, or secret material without enough control evidence. | Run restore drill, record authorization/evidence, validate tenant scope, and document restore-time re-erasure behavior. | Codex/Robert | Open |
| Sponsor/resource-map projection privacy incomplete | New DataHub views may accidentally expose employee-level detail or hidden allocation internals. | Approve projection output shapes and export policies before exposing HR, sponsor, or facilities dashboards. | Codex/Claude | Open |
| Trust boundary diagram missing | Reviewers cannot quickly verify exposed surfaces and data protection boundaries. | Add a target trust-boundary diagram for hosted pilot and client-owned production. | Codex | Open |
