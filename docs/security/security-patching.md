## Security Patching

Security patching keeps FairSpot services, dependencies, containers, and hosted infrastructure within an acceptable vulnerability window. The process is provider-neutral and applies to local, NAS/Cloudflare, DigitalOcean, Kubernetes, and client-owned profiles.

## Identification

- Monitor .NET, npm, container image, Dapr, Keycloak, database, broker, and operating-system advisories.
- Run dependency and image scanning in CI where available.
- Review provider/resource security notices for the selected hosted profile.
- Treat authentication, tenant isolation, Secret exposure, and PII leakage findings as release blockers until triaged.

## Acquisition

- Update NuGet/npm packages through normal dependency management.
- Rebuild containers from patched base images.
- Update runtime images such as Dapr, Keycloak, database, broker, Vault, MinIO, and observability components when the selected profile uses them.
- Obtain host OS patches through the NAS, Droplet, Kubernetes node, or client platform patch process.

## Testing

- Run targeted unit/integration tests for the patched component.
- Run the relevant hosted smoke checklist when auth, ingress, Dapr, persistence, messaging, or secrets are touched.
- Validate login, booking, notification, audit, reporting, and reset flows before promoting a patched hosted profile.

## Deployment

- Deploy immutable image tags, not ad hoc host builds.
- Keep rollback instructions and the previous known-good tag visible.
- Back up state before risky infrastructure or data-store patching.
- Monitor service health, error rate, logs, and traces after rollout.

## Third-Party Docker Images

- Pin image versions where practical.
- Scan images before hosted promotion.
- Rebuild or pull patched images through the selected registry.
- Document exceptions and accepted risks in the gap/waiver process.
