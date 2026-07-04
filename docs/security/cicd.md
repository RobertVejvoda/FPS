# CI/CD Security

CI/CD security protects build inputs, generated artifacts, release evidence, and deployment credentials. FairSpot uses GitHub-hosted repository workflows and profile-specific deployment steps; the security rules remain provider-neutral.

## Pipeline Controls

- Run validation before merge and before hosted promotion.
- Build immutable container images and deploy selected tags.
- Keep secrets in GitHub Secrets or the approved operator secret store, never in repository files.
- Limit deployment tokens to the profile and action they need.
- Record release evidence, image tags, validation results, and rollback instructions.

## Testing and Scanning

- Run unit/integration tests relevant to the changed slice.
- Run web/mobile build or typecheck checks for UI changes.
- Scan dependencies and container images where available.
- Run hosted smoke checks after deployment-profile changes.

## Deployment Strategy

- Prefer repeatable scripts and explicit profile configuration.
- Keep rollback to a previous known-good image tag possible.
- Avoid building on the runtime host for Release 1 and later hosted profiles.
- Require human/Codex review for security, privacy, tenant isolation, secrets, deployment, and production-operations changes.

## Evidence

PRs and release records should include:

- validation commands and results;
- deployed image tags or artifact identifiers;
- migration or config changes;
- smoke-test evidence where hosted behavior changed;
- known gaps or accepted waivers.
