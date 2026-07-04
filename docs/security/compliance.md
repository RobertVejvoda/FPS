## Compliance Checks

Compliance checks verify that FairSpot implementation and hosted profiles follow the security, privacy, audit, and operational controls documented in this repository. They are provider-neutral.

## Check Types

| Check | Purpose |
| --- | --- |
| Security review | Review authentication, authorization, tenant isolation, secrets, logging, audit, and data-handling changes. |
| Code review | Catch security regressions before merge. |
| Dependency and image scanning | Detect vulnerable packages, base images, and runtime components. |
| Penetration testing | Validate internet-facing paths before real customer data. |
| Hosted smoke evidence | Prove login, booking, notification, audit, reporting, WAF/ingress, backup/restore, and reset paths. |
| Access review | Confirm privileged and break-glass access remains justified and time-bound. |

## Compliance Requirements

- **Data protection**: apply minimization, tenant scoping, pseudonymisation, retention, erasure, and encryption controls.
- **Access control**: use role-based authorization and authenticated tenant/user context.
- **Incident response**: follow [Incident Handling](../production/incident-handling).
- **Encryption**: use HTTPS externally and encrypted storage/backups for hosted profiles.
- **Audit trails**: business activity belongs in the Audit service; technical telemetry belongs in the observability platform.
- **Risk management**: record unresolved production-impacting gaps in the security gap register or governance waivers.

## Procedures

1. Define the control or acceptance criterion before implementation.
2. Run the relevant validation and smoke checks.
3. Record evidence in the issue, PR, runbook, or release evidence record.
4. Track gaps with owner, severity, and next action.
5. Re-run checks after material auth, tenant isolation, secrets, deployment, or telemetry changes.
