## Integrity

Integrity controls ensure FairSpot data, events, configuration, and audit evidence are accurate, complete, and protected from unauthorized or accidental modification.

## Strategies

1. **Data validation**
   - Validate input on client and server.
   - Enforce API contracts and domain rules before persistence.

2. **Access controls**
   - Use tenant-scoped authorization and least privilege.
   - Prevent request bodies or query strings from overriding authenticated tenant/user context.

3. **Audit trails**
   - Record policy-sensitive business actions in the Audit service.
   - Keep technical logs separate from business audit records.

4. **State consistency**
   - Use service-owned stores and Dapr/state-store contracts with tenant-safe keys, collections, or partitions.
   - Use transactions, outbox, idempotency keys, or documented retry behavior where business state and events must stay aligned.

5. **Backups and restore**
   - Back up authoritative state and test restore paths.
   - Validate restored data through smoke checks and tenant-scoped verification.

6. **Encryption and key management**
   - Use HTTPS externally and encrypted hosted storage/backups.
   - Keep keys and credentials in an approved secret-management process.

## Dapr Integrity Notes

- Dapr pub/sub consumers must be idempotent.
- Dapr state-store components must be tested per profile before hosted use.
- Dapr resiliency policies should define timeouts, retries, and circuit breakers for dependencies.
- Where transactional outbox is supported and needed, use it for state-plus-event reliability; otherwise document the service-owned pending-event pattern.
