# Auditor &amp; Security Tour

**Who this is for:** an auditor, security reviewer, or data protection officer checking how FairSpot handles trust, evidence, and personal data.

**What matters to you:** the trust boundaries, what evidence exists, how personal data is minimised and pseudonymised, and where the known limits are.

## Trust boundaries

- **Tenant isolation.** Tenant context comes from authenticated or trusted service context. Employee APIs must not accept arbitrary tenant/user values from request bodies, and persistence is tenant-scoped. See the [Security Architecture](./architecture/security/security-architecture).
- **Claim-based identity.** Identity, tenant, and roles are resolved from the sign-in (SSO-first). FairSpot does not store customer IdP passwords; FairSpot-local credential verifiers are fallback Secret data owned by Identity.
- **Data minimisation.** FairSpot stores the minimum user/profile facts needed for booking, notification, audit, reporting, and support — see the [Privacy Architecture](./architecture/security/privacy-architecture).

## Evidence and pseudonymisation

- **Audit.** Append-only audit records preserve allocation and policy-sensitive evidence, using stable/pseudonymised identifiers where possible so the trail exists without unnecessary PII. Auditors can query booking and policy-sensitive actions.
- **Reporting.** Tenant-scoped read models give operational and fairness summaries; user-visible reasons stay safe (no hidden lottery internals, no other users' data).
- **Controls.** The control set is catalogued in [Controls](./architecture/security/controls).

## Privacy and GDPR

- **Erasure.** A subject's identity mapping can be erased while anonymous audit history is preserved; report projections are anonymised for a subject at the durable store. Personal data handling is summarised in the [Client Evaluation Pack → Security &amp; GDPR](./client-evaluation-pack#security-and-gdpr-summary).
- **Known boundaries.** Open, honest gaps — what is implemented, what is production-blocking, and what is a documented manual step — are tracked in the [Security Gap Register](./architecture/security/gap-register). This is the page to read before trusting the platform with real data.

> 📷 **Screenshot gap:** web _Audit query_ surface with pseudonymised actor references — real screen not yet captured.

## Try it in the demo

In **Green Logistics**, sign in as `gl-auditor` (Martin Cerny, `Dev1234!`) to review the audit log for the seeded booking and Draw activity, and note that actor references are pseudonymised. A second **demo** tenant (a bare scaffold with no business data) exists specifically to demonstrate cross-tenant isolation. Detail: [Demo Seed Data](./demo-seed-data).

## The right public security docs

- [Security Architecture](./architecture/security/) — the security model index.
- [Privacy Architecture](./architecture/security/privacy-architecture) and [Controls](./architecture/security/controls).
- [Security Gap Register](./architecture/security/gap-register) — the honest, current list of known gaps.
- [Client Evaluation Pack](./client-evaluation-pack#security-and-gdpr-summary) — the business-facing security and GDPR summary.

Secrets — tokens, keys, client secrets, connection strings, credential verifiers — are Secret data and never appear in docs, logs, issues, or manifests. Detailed hosted-operator security procedures live in the private `fairspot-platform` companion.
