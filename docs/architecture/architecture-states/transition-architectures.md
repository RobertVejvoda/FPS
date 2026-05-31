# Transition Architectures

| Field | Value |
| --- | --- |
| Status | Draft |
| Version | 0.1 |
| Architecture State | Transition |
| ADM Phase | Phases E/F |
| Responsible | Codex/Product Owner |
| Accountable | Robert |
| Last Reviewed | - |
| Next Review | On milestone change |

Transition architectures describe staged movement from current-state evidence toward the customer-ready target.

| Transition | From Version | To Version | Capabilities Added | Risks | Exit Criteria |
| --- | --- | --- | --- | --- | --- |
| T1 Customer-ready docs and architecture governance | Current State v0.1 | Customer-Ready Target v0.1 | TOGAF map, artifact register, architecture states, public docs cleanup. | Documentation can look complete before validation catches up. | Gaps are explicit and client-facing docs avoid maintainer-only workflow. |
| T2 Hosted pilot readiness | Current State v0.1 | Hosted Pilot Target v0.1 | Durable customer state, DataHub read models, Cloudflare/WAF profile, smoke runbooks, role-centered UI. | Runtime and security gaps can block public domain deployment. | Hosted smoke path passes and known exceptions are accepted. |
| T3 Customer production handoff | Hosted Pilot Target v0.1 | Customer-Owned Production Target v1.0 | Client-owned identity, secrets, backup/restore, observability, operations, and support boundary. | Client environment differences can require profile-specific work. | Handoff checklist and recovery evidence are accepted. |
