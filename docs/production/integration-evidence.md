# OPS005 Integration Evidence And Safe Credential Boundaries

This page defines the safe credential handling and operational evidence boundaries for customer-system integration actors in FPS.

## Credential Classification

| Category | Examples | Handling rule |
| --- | --- | --- |
| **Secret** (never logged) | JWT access/refresh tokens, OAuth client credentials, API keys, credential verifiers, private keys, database connection strings | Must not appear in logs, traces, metrics labels, events, or GitHub issues. Store only through the configured Dapr secretstore. No committed values. |
| **Confidential** (log ID only) | Tenant ID, user ID, booking ID, location ID, employee profile facts | Log identifiers only, not record content. Mask or omit in telemetry labels and event payloads. |
| **Internal** (log metadata) | Request duration, error code, retry count, component name | Safe for logs and metrics. No personal data or credential material. |

See `docs/security/security-model.md` for the full data classification model.

## What FPS Services Log

ASP.NET Core's default request logging does **not** log:
- `Authorization` request headers (JWT access tokens)
- Request bodies (which might contain credentials or PII)
- Response bodies

Application code in FPS services logs only:
- Structured events with identifiers (tenant ID, user ID, booking ID) at `Information` level
- Error context with exception type and message; no raw stack traces or upstream system responses that could expose internal URLs or credential material
- Dapr component operation results (success/failure, not credential material)

**Prohibited in any log, trace label, or domain event:**
- Raw JWT tokens or partial token strings
- Password, credential verifier, or OAuth client credential material
- Full personal data fields (email, display name, license plate) unless required for audit and classified as Confidential

## Integration Actor Credential Handling

An integration actor is a non-human FPS caller: a scheduled job, a customer HR import process, a SCIM client, or an API gateway.

| Boundary | Rule |
| --- | --- |
| Credential storage | Integration actor credentials must be stored in the configured Dapr secretstore. No inline credentials in component YAML, appsettings files, or container images. |
| Credential access | Services access credentials only at runtime via the Dapr secretstore reference pattern. Credentials must not be passed between services or included in domain events. |
| Credential logging | Integration actor identity (actor ID or app ID) is safe to log. Credential values, tokens, and keys are not. |
| Credential rotation | Rotation must be supported without downtime. Vault dynamic credentials or short-lived cloud credentials are preferred. Rotation evidence must be recorded in the audit log. |

## Safe Evidence For Integration Operations

Integration operations — SSO claim mapping, profile import, SCIM provisioning, HR feed processing — must produce audit evidence without logging sensitive data.

| Event type | Safe log/audit content | Prohibited content |
| --- | --- | --- |
| SSO user authenticated | `tenantId`, `userId` (from `sub`), mapped role names, timestamp | Raw token value, PKCE code |
| Integration actor request | Actor app ID, endpoint, status code, duration, retry count | Authorization header value |
| Profile fact import | `tenantId`, `userId`, changed fact names (not values), `factSource`, timestamp | Employee PII, import file path with credentials |
| SCIM provisioning | Operation type (create/update/deactivate), `tenantId`, `userId`, timestamp | Full user record, passwords |
| Integration failure | Error code, retry attempt number, source system name | Credential material, full upstream error response body |
| Credential rotation | Actor ID, rotation timestamp, evidence of success | Old or new credential values |

## Retry And Error Evidence

Integration failures must produce evidence for operators without exposing sensitive data.

- Log the error code and category (authentication failure, rate limit, timeout, schema validation), not the raw error response from the upstream system.
- Log retry attempt number and back-off delay, not the full retry context.
- For permanent failures (identity mapping rejection, deactivated user, invalid tenant), log the rejection reason code and the affected `(tenantId, userId)` pair for audit traceability.

## OpenTelemetry Redaction

When configuring an OpenTelemetry collector or exporter for production, exclude authorization-bearing attributes:

```yaml
processors:
  attributes/redact:
    actions:
      - key: http.request.header.authorization
        action: delete
      - key: http.request.header.cookie
        action: delete
```

Apply these rules at the collector level so individual services do not need per-service redaction logic.

## Verification Smoke Commands

Verify no authorization header values appear in local stdout logs:

```sh
# Start harness and check for JWT-like strings in output
./tools/start-local-harness.sh 2>&1 | grep -cE "eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+"
# Expected: 0
```

Confirm health endpoints are reachable and return no sensitive data:

```sh
curl -s http://localhost:5192/health | python3 -m json.tool
curl -s http://localhost:5131/health | python3 -m json.tool
```
