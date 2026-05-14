[Audit module](../application-layer/audit) is designed to ensure the integrity, availability, and confidentiality of audit logs within the system. It provides mechanisms for logging, retaining, protecting, and reviewing audit logs to comply with regulatory requirements and enhance security monitoring.

### Audit Services

#### Log Management Service
- **Functions:**
    - Log Capture and Storage
    - Log Backup Management
    - Retention Management
- **Processes:**
    - Log Collection Process
    - Archive Process
    - Cleanup Process
- **Events:**
    - Log Entry Created
    - Backup Completed
    - Retention Period Exceeded

#### Security Control Service
- **Functions:**
    - Access Control Management
    - Log Protection
    - Integrity Verification
- **Processes:**
    - Access Validation Process
    - Integrity Check Process
- **Events:**
    - Unauthorized Access Attempt
    - Integrity Breach Detected

#### Monitoring Service
- **Functions:**
    - Log Analysis
    - Threat Detection
    - Review Management
- **Processes:**
    - Automated Analysis Process
    - Security Review Process
- **Events:**
    - Suspicious Activity Detected
    - Review Completed

#### Compliance Service
- **Functions:**
    - Regulatory Compliance
    - Audit Support
    - Trail Management
- **Processes:**
    - Compliance Verification Process
    - Report Generation Process
- **Events:**
    - Compliance Check Initiated
    - Audit Report Generated

#### Access Control Service
- **Functions:**
    - User Authorization
    - Access Level Management
    - Access Monitoring
- **Processes:**
    - Access Request Process
    - Authorization Process
- **Events:**
    - Access Granted
    - Access Denied
    - Authorization Changed

## Slice A001: Booking Audit Consumer

A001 is the first Audit implementation slice. It consumes Booking events and stores append-only audit records with pseudonymised actors.

A001 must:

- subscribe to the Booking event topic;
- accept the Booking event envelope defined in [Booking Event Contracts](./booking-event-contracts);
- store one audit record per unique `eventId`;
- deduplicate duplicate deliveries by `eventId`;
- record tenant, event type, event version, business timestamp, ingestion timestamp, correlation ID, causation ID, source, actor type, actor hash, entity reference, and captured payload;
- pseudonymise user references before writing audit records;
- tolerate additive Booking event payload fields;
- keep the repository interface append-only for this slice.

A001 must not:

- expose `GET /audit` or any audit query API;
- implement GDPR erasure or `DELETE /pii-mapping/{userId}`;
- persist the full `PiiMapping` store beyond a documented shape if needed;
- add audit UI, reporting dashboards, retention jobs, backup jobs, or integrity verification jobs;
- change Booking event publication or Booking state transitions;
- store raw names, emails, actor IDs, requestor IDs, or profile private data in Audit records.

Minimum audit record fields:

| Field | Meaning |
| --- | --- |
| `auditRecordId` | Internal Audit record ID. |
| `sourceEventId` | Booking event ID and idempotency key. |
| `eventType` | Booking event type. |
| `eventVersion` | Booking event schema version. |
| `occurredAt` | Timestamp from the Booking event. |
| `recordedAt` | Audit ingestion timestamp. |
| `tenantId` | Tenant that owns the event. |
| `correlationId` | Request/workflow correlation ID. |
| `causationId` | Command, workflow activity, or source event. |
| `actorType` | Actor category from the Booking event. |
| `actorHash` | SHA-256 hash of the actor ID when present. |
| `source` | Producing service, normally `booking`. |
| `entityType` | Stable category such as `bookingRequest`, `drawAttempt`, or `penalty`. |
| `entityId` | Primary entity ID when known. |
| `payload` | Captured payload with raw user identifiers removed or replaced by hashes. |

Pseudonymisation rules:

- Store `actorHash`, not raw `actorId`.
- Store requestor and affected-user hashes, not raw user IDs.
- Use a deterministic SHA-256 implementation so repeated events for the same source ID can be correlated without exposing identity.
- The `PiiMapping` shape is `{ actor_hash, user_id, name, email }`, but A001 does not persist or erase this mapping.
- On a future GDPR erasure request, the mapping row is deleted while append-only audit records remain anonymous.

Acceptance criteria:

- Given a valid Booking event, Audit stores one append-only audit record.
- Given the same event is delivered twice, Audit stores one record.
- Given a Booking event has an actor ID, the audit record contains `actorHash` and no raw actor ID.
- Given a payload has requestor or affected recipient IDs, the stored payload contains hashed references and no raw user IDs.
- Given the event has no actor ID, Audit still records the event with actor type/source and a null actor hash.
- A001 tests prove the repository abstraction has no update or delete path for audit records.
