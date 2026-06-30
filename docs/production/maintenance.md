# Maintenance

> **Private-later (#670):** hosted-platform-operator runbook — planned to move to the private `fairspot-platform` repository. This slice only classifies it; the [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary.md) tracks the public summary/replacement that will accompany the move. Nothing is moved or deleted here.

Maintenance keeps a FairSpot environment secure, recoverable, observable, and cost-controlled after deployment. The same maintenance responsibilities apply across local, demo, and client-owned production profiles, but the concrete tooling is selected by the environment.

## Maintenance Areas

| Area | Requirement |
| --- | --- |
| Runtime platform | Patch the selected container/runtime platform, host images, Dapr runtime, ingress, certificates, and deployment tooling on a planned cadence. |
| Application services | Deploy versioned service images, run smoke checks after each release, and keep rollback images/configuration available. |
| Dapr components | Validate component health, logical component names, secret references, pub/sub subscriptions, state-store access, and mTLS/service-identity settings where used. |
| Identity provider | Review issuer/audience configuration, signing-key rotation, role/group mappings, tenant claim mapping, and deactivated-user behavior. |
| Data stores | Maintain indexes, tenant storage scopes, backup schedules, restore drills, retention policies, and storage-capacity alerts. |
| Broker/provider | Monitor event delivery, retries, dead-letter queues or equivalent failure surfaces, duplicate handling, and subscriber lag. |
| Secret management | Rotate credentials and certificates, review access records, remove stale secrets, and verify that manifests do not contain inline secret values. |
| Observability | Maintain dashboards, alert rules, log/trace retention, sampling, and correlation IDs. Tooling must remain OpenTelemetry-compatible where telemetry leaves the application. |
| Object storage | Validate tenant-scoped paths, encryption, lifecycle policies, backup/export retention, and restore procedures for reports or attachments. |
| Cost controls | Review idle services, replica minimums, log volume, trace sampling, storage growth, and demo teardown/scale-down procedures. |

## Change Control

Production or demo maintenance should be handled as a controlled change:

1. Record the target environment, owner, reason, planned window, and rollback path.
2. Validate backups, secret access, and known-good image/configuration versions before the change.
3. Apply runtime, component, or application changes through repeatable scripts or deployment pipeline steps.
4. Run post-change checks for login, tenant context, booking read/write, pub/sub consumers, audit ingestion, notification reads, and observability.
5. Record evidence, defects, and follow-up actions.

## Minimum Recurring Checks

| Frequency | Check |
| --- | --- |
| Daily for production, before each demo for demo | Health checks, failed background work, broker/provider delivery errors, authentication errors, and alert routing. |
| Weekly | Storage growth, backup completion, log/trace volume, cost drift, stale secrets, and failed scheduled jobs. |
| Monthly | Restore drill evidence, access review for privileged roles, dependency/runtime patch review, certificate expiry review, and tenant provisioning checks. |
| Before client pilot/go-live | Full smoke test, backup/restore drill, incident runbook rehearsal, identity claim validation, and security review sign-off. |

Provider-specific maintenance commands belong in the selected deployment profile or client runbook, not in the core architecture pages.
