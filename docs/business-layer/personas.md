## Andrea - Occasional Car Commuter

Andrea normally uses public transport but needs to drive to the office a few times per week. In the current process, Andrea must email HR and wait for a response. The result feels unpredictable, especially when daily parkers or faster requestors seem to receive spaces more often.

### Customer Value

Andrea needs a simple way to request parking and understand the result. FairSpot creates value by making the process transparent, reducing manual follow-up, and giving occasional parkers a fair chance to receive a space.

### Key Needs

- Submit parking requests without emailing HR.
- See request status and allocation results.
- Receive timely notifications and reminders.
- Trust that allocation rules are applied consistently.

## Bob - Motorcycle Commuter

Bob sometimes comes to the office by motorcycle. Standard car spaces are inefficient for motorcycles and may increase the risk of damage. Bob needs the system to recognize motorcycle-specific capacity instead of treating every vehicle as a car.

### Customer Value

FairSpot helps customers use parking space more efficiently by supporting vehicle-specific rules. Dedicated motorcycle handling could increase total usable capacity and improve employee experience without adding new parking spaces, but it is a future optional extension rather than a v1 requirement.

### Future / Optional Needs

- Register a motorcycle as a vehicle.
- Request motorcycle-appropriate parking.
- Use dedicated motorcycle spaces or shared-capacity rules.
- Receive the same clear status updates as car users.

## Cecil - Company-Car User

Cecil has a company car and may have access to a reserved space. When Cecil does not need the space, the company wants to release it for other employees instead of leaving it unused.

### Customer Value

FairSpot protects company-car entitlements and lets company-car users participate in the governed booking flow. Reserved-space release automation could turn unused entitlement into reusable capacity, but recurring release workflows are a future optional extension.

### Key Needs

- Maintain company-car eligibility where policy requires it.
- Avoid unnecessary HR coordination through standard company-car booking.

### Future / Optional Needs

- Declare when a reserved space is not needed.
- Automate recurring requests or releases.

## David - Employee Using Informal Arrangements

David sometimes asks colleagues to use their reserved spaces or parks without a formal reservation. This behavior is a symptom of an unclear process: employees do not trust that the official route will work, so they create side agreements.

### Customer Value

FairSpot reduces informal parking behavior by making the official process easier, faster, and more transparent. The business benefit is fewer disputes, better traceability, and more reliable occupancy data.

### Key Needs

- See whether legitimate parking is available.
- Use a fast request path for same-day spaces.
- Understand why a request was accepted or rejected.
- Avoid relying on private agreements outside policy.

## Elvis - HR Parking Coordinator

Elvis manages parking requests for employees. Today this means reading emails, updating a spreadsheet, answering follow-up questions, and resolving complaints about fairness. The workload grows with every location, exception, and policy change.

### Customer Value

FairSpot removes HR from routine request processing. Elvis can focus on policy, exceptions, and employee support instead of manually matching people to spaces.

### Key Needs

- Centralize all requests and allocations.
- Reduce manual email handling.
- Handle exceptions with clear justification.
- Review audit history when disputes occur.
- Access reports on demand, utilization, and no-shows.
- Start from an attention queue that shows what needs HR action today.
- Search an employee request by safe business reference and explain the outcome without exposing hidden Draw internals.
- Make manual corrections only when policy allows it, with a required reason and audit record.

## Fiona - HR Manager

Fiona is accountable for employee experience and fair workplace policies. She needs evidence that parking is allocated consistently and that limited capacity is used responsibly.

### Customer Value

FairSpot gives Fiona management visibility. Reports and audit trails help her explain policy decisions, identify capacity problems, and improve employee trust.

### Key Needs

- View allocation fairness and utilization reports.
- Identify peak demand and repeated shortages.
- Track cancellations, no-shows, and policy exceptions.
- Support decisions with data instead of anecdotes.
- Preview policy or capacity impact before changing rules.
- Use trends to decide whether a location needs more capacity, different zone rules, or employee communication.

## Freya - Facilities Coordinator

Freya owns the physical resource map: locations, areas, spaces, temporary closures, EV chargers, accessibility spaces, and capacity changes. HR may own the policy, but facilities often owns whether the mapped capacity reflects the real site.

### Customer Value

FairSpot helps Freya keep the physical capacity model accurate without needing to understand allocation internals. Accurate locations, zones, and capacity prevent false promises to employees and reduce manual corrections after a Draw.

### Key Needs

- Maintain locations, zones, capacity pools, and space availability.
- Mark spaces or areas unavailable for maintenance or temporary business events.
- Keep EV, accessibility, company-car, and reserved-space capabilities accurate.
- See utilization and shortage patterns by location, zone, and capability.
- Publish resource-map changes with validation and audit evidence.

## Astrid - Auditor / Compliance Reviewer

Astrid reviews whether FairSpot was operated consistently, whether sensitive actions are traceable, and whether privacy obligations are respected. She is not trying to run parking operations; she needs defensible evidence.

### Customer Value

FairSpot gives Astrid an evidence trail for allocation outcomes, manual overrides, policy changes, actor-resolution lookups, retention actions, and erasure workflows. The audit view should make it possible to prove integrity without exposing unnecessary personal data or technical logs.

### Key Needs

- Search business activity by date range, action, actor hash, request reference, policy version, result, and reason code.
- Review the lifecycle of a request, Draw, policy change, manual correction, retention job, or erasure workflow.
- Verify that manual actions include a reason and authorized actor.
- Resolve an actor only through an approved, reasoned, audited permission path.
- Export safe audit evidence without raw lottery internals, raw user IDs, secrets, stack traces, or unrelated employee data.
- Link business audit records to technical trace IDs for support escalation without exposing Grafana/Loki logs in the business UI.

## Ada - Customer / IT Administrator

Ada is responsible for making the customer environment work: identity, role mapping, tenant readiness, integrations, branding, storage, and operational setup. Employees and HR should not see technical tenant concepts, but Ada needs them in the administration context.

### Customer Value

FairSpot gives Ada a controlled setup surface for onboarding and operating the company instance. The admin experience should show readiness, integration health, and configuration gaps before employees depend on the system.

### Key Needs

- Configure identity provider integration and role mappings.
- Review readiness checks for lifecycle state, admins, policy, locations, profile facts, booking smoke tests, notification reachability, audit evidence, reporting evidence, and tenant object storage.
- Manage company branding and business display names without exposing technical IDs to employees.
- Configure tenant-scoped storage and integration endpoints through safe admin workflows.
- See operational setup issues and suggested next actions.
- Keep admin actions auditable and separate from employee-facing terminology.

## Godric - Executive Sponsor

Godric cares about operational efficiency, employee satisfaction, and responsible use of company resources. Parking may not be a core business process, but poor parking management creates daily friction and unnecessary HR cost.

### Customer Value

FairSpot converts a recurring workplace frustration into a governed, measurable service. The executive value is lower administration cost, fewer employee complaints, better asset utilization, and optional support for sustainability goals.

### Key Needs

- Reduce organizational friction around parking.
- Improve utilization of existing parking assets.
- Support transparent and defensible policy.
- Measure impact through reports and trends.
- Extend policy toward sustainability when the business is ready.
- See a concise management summary rather than operational detail.
- Understand HR effort saved, unmet demand, fairness trend, and capacity pressure.
- Use evidence to decide whether FairSpot should expand beyond parking into other limited workplace resources.
