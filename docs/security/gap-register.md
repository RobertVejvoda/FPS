# Security Gap Register

This register records known security and privacy gaps that are not yet implemented or fully evidenced. Each entry is factual, issue-ready, and ordered by area. Gaps labelled **production-blocking** must be resolved before any client-owned production deployment.

Last updated: 2026-05-24.

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
| Service-to-service hardening is not complete across all internal APIs | Low | Privacy erasure service endpoints are protected with Dapr app API token validation (`APP_API_TOKEN` / `dapr-api-token`). Other internal calls still rely on Dapr invocation and network boundary. Dapr mTLS is disabled in local mode and must be enabled for production. See BYOC responsibility. |

---

## Tenant Isolation

| Gap | Severity | Notes |
|-----|----------|-------|
| No infrastructure-layer tenant isolation (shared stores) | Medium — production-blocking | Current design uses application-layer tenant keys in shared MongoDB and in-memory stores. Production should use per-tenant collections, schemas, or separate stores depending on contract. Contract defined in [Tenant Storage Contract](../production/tenant-storage-contract.md). |
| Booking Dapr state keys missing tenant prefix (`request:`, `penalty:`, `correction:`) | **Implemented (DATA010)** | Migrated to `request:{tenantId}:{id}`, `penalty:{tenantId}:{id}`, `correction:{tenantId}:{...}` via `TenantStorageKey.For()`. All Booking/Penalty/Correction repositories updated. See [Tenant Storage Contract](../production/tenant-storage-contract.md) § Implementation Gaps. |
| In-memory repositories share process memory across tenants | Low | Affects local/demo mode only. No persistence, data is lost on restart. Not a production concern but should not be used in a pilot with multiple active tenants. |
| No shared TenantStorageKey sanitisation helper | **Implemented (DATA010)** | `FPS.Booking.Infrastructure.TenantStorageKey` validates character set, length, reserved prefixes, and normalises to lowercase. All Booking key-type repositories use it. Unit tests in `TenantStorageKeyTests`. |

---

## Encryption

| Gap | Severity | Notes |
|-----|----------|-------|
| Encryption at rest not configured | High — production-blocking | Services rely on infrastructure storage defaults. Client must enable encryption at rest on all stores (MongoDB, object storage). FairSpot does not configure this; it is a client deployment responsibility. |
| TLS for internal service-to-service traffic only via Dapr mTLS | Medium — production-blocking | Dapr mTLS is disabled in local config (`fps-config.yaml`). Must be enabled in production. Client is responsible for Dapr trust anchor and mTLS configuration. |

---

## Secret Management

| Gap | Severity | Notes |
|-----|----------|-------|
| Vault is unsealed with a dev root token in local config | Low | Local/demo only. Production must use a hardened Vault or equivalent secret store with proper unseal and access-control policies. |
| Secret rotation is not automated | Medium | Documented as customer responsibility. No tooling or rotation schedule is provided by FairSpot. |

---

## GDPR / Data Privacy

| Gap | Severity | Notes |
|-----|----------|-------|
| Employee data erasure workflow needs durable store completion | High — production-blocking | PRIV001 is implemented: authorised privacy/admin users can create an erasure request; Dapr Workflow coordinates service-owned steps; notification deletion, reporting anonymisation, Audit PII mapping deletion, per-step audit records, and APP_API_TOKEN-protected internal erasure endpoints are in place. Production remains blocked until Profile erasure and Booking active-check/anonymisation move beyond local/durable-store stubs and have smoke evidence against the selected production store. |
| Booking and notification retention jobs not implemented | High — production-blocking | `DELETE /audit/retention` is implemented (A004), but automated retention enforcement for booking history and notification records is not yet implemented. Client-approved retention periods and scheduled jobs are required before production processing of personal data. |
| No consent or privacy notice flow in the product | Medium | Privacy notice delivery is a legal/UX responsibility outside the product. FairSpot does not display or record consent. Client must implement at the IdP or application layer as required by their legal basis. |
| DPIA not completed | Medium | A Data Protection Impact Assessment is required before production processing of personal data in most GDPR jurisdictions. This is a client/legal responsibility. |

---

## Audit

| Gap | Severity | Notes |
|-----|----------|-------|
| Audit integrity verification: no cryptographic chaining | Low | `GET /audit/integrity` and `GET /audit/export` are implemented (A005). Records are append-only but not cryptographically chained. Sufficient for audit evidence; production hardening may require external signing. |
| Audit retention: client must schedule invocation | Low | `DELETE /audit/retention` is implemented (A004). Client configures retention period and schedules periodic invocation. |
| Reporting data projection lag not measured | Low | Reporting read models are updated on event consumption. No lag monitoring or alerting is wired. |

---

## Observability

| Gap | Severity | Notes |
|-----|----------|-------|
| Production log/metric forwarding is client responsibility | Medium | OBS001 (OTel traces), OBS002 (Prometheus metrics + Grafana), OBS003 (alert rules), OBS004 (local Loki ingestion), and OBS005 (safe application log coverage) are implemented locally. Client must configure log shipping to SIEM and connect their monitoring platform via OTLP or Prometheus remote-write. |
| Production alert thresholds need client tuning | Low | Basic alert rules (service down, high error rate, latency, RabbitMQ) are in place (OBS003). Production thresholds and notification destinations (PagerDuty, Slack, email) are client configuration. |

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
