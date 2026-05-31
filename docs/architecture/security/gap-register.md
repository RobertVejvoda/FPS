# Security Gap Register

This page summarizes architecture-significant security gaps. Detailed security gaps remain in the legacy [Security Gap Register](/security/gap-register) until migrated.

| Gap | Impact | Mitigation | Owner | Status |
| --- | --- | --- | --- | --- |
| Hosted pilot end-to-end validation incomplete | Public deployment may expose unexpected runtime/auth/WAF issues. | Run hosted smoke, WAF, auth, and internal-path validation before public demo. | Codex/Robert | Open |
| Customer durable tenant state incomplete | Tenant setup may not survive restart where in-memory repositories remain. | Implement Customer durable state and validate restart behavior. | Claude/Codex | Open |
| DataHub projection privacy shape incomplete | Reports/read models may expose too much or too little detail. | Define first projection catalog and role-safe output shapes. | Codex/Claude | Open |
