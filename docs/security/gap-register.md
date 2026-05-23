# Security Gap Register

This register records known security and privacy gaps that are not yet implemented or fully evidenced. Each entry is factual, issue-ready, and ordered by area. Gaps labelled **production-blocking** must be resolved before any client-owned production deployment.

Last updated: 2026-05-23.

---

## Authentication

| Gap | Severity | Notes |
|-----|----------|-------|
| Token refresh not implemented in web/mobile clients | Medium | Access tokens expire; users must re-login. Acceptable for demo; production requires refresh token handling or short session tolerance. |
| Dev-token fallback has no server-side expiry | Low | The dev-fallback path is local-only and disabled by default. No production exposure. Track as a code hygiene item. |

---

## Authorization

| Gap | Severity | Notes |
|-----|----------|-------|
| No rate limiting on authentication endpoints | Medium | Envoy can enforce rate limits but no policy is configured. Brute-force risk on local-account and OIDC callback paths. |
| Service-to-service calls use no explicit auth (Dapr invocation only) | Low | Internal service mesh via Dapr. mTLS disabled in local mode; must be enabled for production. See BYOC responsibility. |

---

## Tenant Isolation

| Gap | Severity | Notes |
|-----|----------|-------|
| No infrastructure-layer tenant isolation (shared stores) | Medium — production-blocking | Current design uses application-layer tenant keys in shared MongoDB and in-memory stores. Production should use per-tenant collections, schemas, or separate stores depending on contract. Planned in OPS008. |
| In-memory repositories share process memory across tenants | Low | Affects local/demo mode only. No persistence, data is lost on restart. Not a production concern but should not be used in a pilot with multiple active tenants. |

---

## Encryption

| Gap | Severity | Notes |
|-----|----------|-------|
| Encryption at rest not configured | High — production-blocking | Services rely on infrastructure storage defaults. Client must enable encryption at rest on all stores (MongoDB, object storage). FPS does not configure this; it is a client deployment responsibility. |
| TLS for internal service-to-service traffic only via Dapr mTLS | Medium — production-blocking | Dapr mTLS is disabled in local config (`fps-config.yaml`). Must be enabled in production. Client is responsible for Dapr trust anchor and mTLS configuration. |

---

## Secret Management

| Gap | Severity | Notes |
|-----|----------|-------|
| Vault is unsealed with a dev root token in local config | Low | Local/demo only. Production must use a hardened Vault or equivalent secret store with proper unseal and access-control policies. |
| Secret rotation is not automated | Medium | Documented as customer responsibility. No tooling or rotation schedule is provided by FPS. |

---

## GDPR / Data Privacy

| Gap | Severity | Notes |
|-----|----------|-------|
| Full employee data erasure path not implemented | High — production-blocking | Audit erasure (pseudonymisation) is implemented. Erasure of profile facts, booking history, and notification records is documented but not fully wired end-to-end. Needs a coordinated erasure flow across Profile, Booking, Notification, and Audit services. |
| Retention schedules not implemented | High — production-blocking | Retention periods for bookings, notifications, audit records, backups, and PII mappings are not enforced by any scheduled job. Documented as a follow-up gap (A004 Audit Retention Job exists; booking/notification retention not yet sliced). |
| No consent or privacy notice flow in the product | Medium | Privacy notice delivery is a legal/UX responsibility outside the product. FPS does not display or record consent. Client must implement at the IdP or application layer as required by their legal basis. |
| DPIA not completed | Medium | A Data Protection Impact Assessment is required before production processing of personal data in most GDPR jurisdictions. This is a client/legal responsibility. |

---

## Audit

| Gap | Severity | Notes |
|-----|----------|-------|
| Audit integrity verification not implemented | Medium | Audit records are append-only in the current store but are not cryptographically chained or externally verified. Planned in A005. |
| Audit retention job not implemented | Medium | Old audit records are never deleted. Planned in A004. |
| Reporting data projection lag not measured | Low | Reporting read models are updated on event consumption. No lag monitoring or alerting is wired. |

---

## Observability

| Gap | Severity | Notes |
|-----|----------|-------|
| Prometheus metrics not yet emitted by .NET services | Medium | OBS001 adds OTel traces. OBS002 (this sprint) adds metrics. Until OBS002 merges, there are no application-level Prometheus scrape targets. |
| Log shipping to SIEM not configured | Medium | Services emit structured stdout. Shipping to a client SIEM is a client deployment responsibility. No Fluent Bit or log shipper config is provided. |
| Alerting rules not configured | Medium | Alertmanager and Prometheus are in docker-compose but no alert rules are defined. Planned in OBS003. |

---

## CI/CD and Supply Chain

| Gap | Severity | Notes |
|-----|----------|-------|
| Container image signing not implemented | Medium | Images are built in CI but not signed. Client should verify digest or implement image signing before production. |
| Dependency vulnerability scanning is manual | Low | NuGet NU1902 warnings appear on restore. No automated SBOM or CVE gate in CI. |
| No SBOM published | Low | Software Bill of Materials is not generated or published per release. |

---

## Network Security

| Gap | Severity | Notes |
|-----|----------|-------|
| No Web Application Firewall (WAF) in current deployment profile | Medium — production-blocking | Envoy handles ingress but no WAF or DDoS protection layer is configured. Client must add WAF in production. |
| Envoy CORS policy allows broad origins in local config | Low | Local-only config. Production Envoy config must restrict `allowed_origins` to the deployed web origin only. |
