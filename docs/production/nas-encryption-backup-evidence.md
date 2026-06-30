# NAS Encryption And Backup Evidence

> **Moved private (#684):** the detailed hosted-operator evidence checklist now lives in the private `fairspot-platform` repository at `docs/runbooks/nas-encryption-backup-evidence.md`.

This public page records the security evidence contract only. NAS-specific device settings, backup targets, restore commands, operator identities, and evidence rows are private platform operations material.

## Public Contract

| Area | Requirement |
| --- | --- |
| Data classification | Hosted, pilot, and client-owned profiles treat tenant and employee data as Confidential by default. |
| Encryption in transit | Public traffic uses HTTPS. Private service connectivity uses Dapr mTLS, encrypted tunnels, or an approved equivalent for the selected profile. |
| Encryption at rest | State stores, read models, object storage, and backup artifacts are encrypted where the selected component supports it. |
| Secret handling | Credentials, tunnel tokens, keys, certificates, and recovery material are Secret data and stay out of Git and logs. |
| Restore evidence | Real customer data must not be processed until backup and restore evidence has been captured for the selected profile. |

## Public References

- [Encryption](../security/encryption)
- [Security Architecture](../architecture/security/)
- [Backup And Restore](./backup-restore)
- [Open-Core Documentation Boundary](../strategy-layer/open-core-boundary)
