## Backup and Recovery Security

Backup and recovery controls protect FairSpot state, identity configuration, object storage, release artifacts, and operational evidence. The exact tooling depends on the deployment profile.

## Scope

| Asset | Backup Expectation |
| --- | --- |
| Container images | Immutable image tags in the selected registry; previous known-good tag available for rollback. |
| Databases/state stores | Service-specific backups with tenant scope understood and restore tested. |
| Identity provider | Realm/client/user/role configuration export or provider-approved recovery path. |
| Object storage | Tenant object storage backups or replication according to retention requirements. |
| Secrets | Secret recovery process documented without exposing secret values in docs or tickets. |
| Observability/evidence | Retain enough logs, metrics, traces, and release evidence for incident and audit needs. |

## Release 1 and DigitalOcean Path

- NAS/Cloudflare profile uses the NAS backup/restore responsibility model plus service-level backups.
- DigitalOcean Droplet profile may use Droplet snapshots plus service-specific database/object-storage backups.
- Managed databases or Spaces must use their own backup/retention features if selected.

## Recovery Requirements

- Test restore before customer data is processed.
- Document the restore operator, approval, scope, source backup, and validation result.
- Re-run smoke tests after restore.
- Reapply GDPR erasure where restored backups reintroduce previously erased personal data.
- Keep backup encryption keys and restore credentials in an approved secret-management process.
