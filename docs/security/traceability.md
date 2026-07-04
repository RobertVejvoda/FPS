## Security Traceability

Security traceability records who changed or accessed sensitive systems, what data or resource was affected, and which evidence supports investigation. It is provider-neutral and applies across NAS/Cloudflare, DigitalOcean, Kubernetes, and client-owned profiles.

## Evidence Streams

| Evidence | Purpose |
| --- | --- |
| Audit service records | Business activity, policy-sensitive changes, allocation outcomes, erasure requests, and privileged reads. |
| Technical logs | Service startup, dependency failures, request failures, retries, and support diagnostics. |
| Metrics and traces | Latency, error rate, dependency behavior, and cross-service request correlation. |
| Identity provider logs | Login, MFA, token issuance, and account lifecycle events where exposed by the IdP. |
| Ingress/WAF logs | Public request filtering, rate limiting, and suspicious access attempts. |
| Change records | Pull requests, release evidence, deployment history, and operator actions. |

## Required Correlation

- Business events should include stable event IDs and tenant scope.
- Technical telemetry should carry trace/correlation IDs where available.
- Business-facing audit views must read from the Audit service, not raw logs.
- Support correlation IDs are diagnostic links only; they do not replace authorization or tenant scoping.

## Incident Use

During an incident, operators should be able to reconstruct:

1. who or what initiated the action;
2. the tenant and resource affected;
3. the request, event, or deployment identifier;
4. technical symptoms and dependency behavior;
5. the remediation and follow-up actions taken.
