# Architecture Glossary

|  |  |
| --- | --- |
| **Status** | Draft |
| **Version** | 0.1 |
| **Architecture State** | Cross-cutting |
| **ADM Phase** | Preliminary / Requirements Management |
| **Responsible** | Codex/Product Owner |
| **Accountable** | Robert |
| **Last Reviewed** | 2026-06-02 |
| **Next Review** | On terminology or domain-language change |

This page defines architecture-layer terms used across FairSpot architecture artifacts. The product-facing companion remains [Glossary](/glossary).

## Terms

| Term | Meaning | Context | Preferred Usage | Avoid / Notes |
| --- | --- | --- | --- | --- |
| Draw | Automated allocation process that assigns limited parking capacity to eligible requests under approved business rules. | Business / Application | Use for the auditable allocation run, whether scheduled or controlled manual. | Avoid calling every booking action a Draw. |
| Allocation | Result of a Draw or direct rule assigning a spot, capacity slot, or compatible resource to a request. | Business / Data | Use for the assignment outcome. | Keep separate from Booking Request. |
| Booking Request | Employee expression of intent to use a resource for a date/time/location. | Business / API | Use before the request is allocated or rejected. | It is not proof of a reserved spot. |
| Tenant | Organization unit that owns data boundary, configuration, identity scope, policy, and operational evidence. | Security / Data | Use when discussing isolation, onboarding, configuration, and support boundaries. | Do not accept tenant identity from request bodies. |
| DataHub | Cross-service read-model component fed by events and used for operational views, dashboards, and reports. | Information Systems | Use for CQRS read models that do not own command state. | Do not make DataHub the source of truth for writes. |
| Projection | Event-fed read model maintained by DataHub or another read-side component. | Data / Reporting | Use for query-optimized state rebuilt or repaired from events. | Projection state is not authoritative command state. |
| Event Inbox | Idempotent durable event receiver that records delivery and dispatch/projection status. | Integration / DataHub | Use to protect at-least-once pub/sub consumers from duplicate processing and lost failures. | Do not mark events processed before handlers complete. |
| Deployment Profile | Named runtime, ingress, secrets, storage, and observability configuration for an environment. | Technology / Operations | Use for local, NAS/Cloudflare hosted pilot, demo, and client-owned production profiles. | Avoid mixing product target with profile-specific operations. |
| Architecture Board | Accountable governance body or named owner for FairSpot architecture decisions. | Governance | Use for Robert or a lightweight review group accepting architecture direction. | Does not imply a heavyweight committee for every small change. |

## Term Lifecycle

| Status | Meaning |
| --- | --- |
| Proposed | Term is being introduced or clarified. |
| Accepted | Term should be used consistently across architecture artifacts. |
| Deprecated | Term remains for history but should not guide new content. |
