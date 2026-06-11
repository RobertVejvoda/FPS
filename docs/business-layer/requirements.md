## Business Context

Many companies have fewer parking spaces than employees who want to use them. When parking is managed by email, spreadsheets, or informal agreements, HR becomes the bottleneck and employees perceive the process as arbitrary. First-come, first-served allocation is simple, but it rewards speed and insider knowledge rather than actual business need or fair access.

FairSpot replaces manual parking coordination with a transparent, automated allocation process. Employees request parking for specific time slots, the system allocates available capacity using configurable fairness rules, and all affected users receive clear status updates. The customer value is reduced administration, higher employee trust, better parking utilization, and auditable policy enforcement.

## Business Goals

1. **Reduce HR workload**
   - Remove email-based request handling.
   - Minimize manual corrections, exceptions, and follow-up messages.
   - Give HR and facility managers a single view of demand, allocation, and usage.

2. **Increase perceived fairness**
   - Apply the same allocation rules to every eligible employee.
   - Give occasional parkers a fair opportunity instead of favoring daily users.
   - Make allocation results explainable and auditable.

3. **Improve parking utilization**
   - Reuse spaces released by cancellations or unused reservations.
   - Support half-day and full-day time slots.
   - Track actual usage so the customer can identify underused capacity.

4. **Support company policies**
   - Respect company-car rules, accessibility needs, EV charging, and other configured local constraints.
   - Allow each customer tenant to configure rules without changing the product.
   - Treat motorcycle-specific capacity and recurring reserved-space release as future optional extensions, not v1 commitments.

5. **Improve employee experience**
   - Let employees request, cancel, and track parking without contacting HR.
   - Notify users when requests are submitted, allocated, rejected, cancelled, or reallocated.
   - Integrate with calendars and reminders where it reduces missed reservations.

6. **Provide management insight**
   - Report on demand, allocation fairness, utilization, cancellations, no-shows, and policy exceptions.
   - Help HR and management make evidence-based decisions about parking capacity and commuting policy.

## Core Business Requirements

### BR001: Automated Parking Requests

Employees must be able to submit parking requests through FairSpot instead of email. A request must identify the employee, vehicle where needed, preferred date, time slot, location, and relevant parking requirements.

### BR002: Fair Slot Allocation

FairSpot must allocate limited parking capacity using transparent and configurable fairness rules. The process must avoid persistent favoritism and should improve the chance of allocation for employees who have received fewer recent spaces.

### BR003: Configurable Parking Policies

Customers must be able to configure parking rules for their organization, including locations, spaces, time slots, eligibility, company-car handling, EV charging, accessibility needs, and penalties. Motorcycle-specific capacity and recurring reserved-space release are not v1 requirements; they may be added later if a customer need justifies the added policy and capacity complexity.

### BR003A: Resource Map and Zone Preferences

Customers should be able to upload or maintain a map of allocatable resources, such as parking spaces, desks, chairs, seats, lockers, or chargers. The map should support zones so the allocation process can prefer an employee's requested zone or team default area before falling back to another suitable resource when preferred capacity is unavailable.

Rules:

- a resource map defines locations, zones, individual resources or capacity pools, and resource capabilities;
- zones can represent floors, office areas, parking sections, team neighborhoods, accessibility areas, charger areas, or other customer-defined groupings;
- employees may express a preferred zone when requesting a resource;
- teams or departments may have default zones that are preferred but not absolute reservations unless policy marks them as reserved;
- allocation should first try preferred and default zones, then fall back to any compatible available resource when policy allows;
- fallback allocation must be visible to the employee as a valid allocation outside the preferred zone, not as a policy error;
- strict requirements such as accessibility, vehicle capability, time availability, and reserved-only restrictions must still win over preferences.

### BR004: Real-Time Status and Notifications

Employees must receive clear status updates for request submission, allocation outcome, cancellation, reallocation, and reminders. Notifications should be available in the application and may also be delivered by email, push notification, or calendar integration.

### BR005: Cancellation and Reallocation

Employees must be able to cancel requests and reservations. If a confirmed space becomes available, FairSpot should offer or allocate it to another eligible employee according to customer policy.

### BR006: Usage Confirmation

FairSpot should support confirmation of actual parking usage, either by user action, access-control integration, QR code, card reader, or another customer-specific signal. Usage data is required for fairness, penalties, and reporting.

### BR007: Penalties and Adjustments

FairSpot must support configurable penalties for late cancellations, no-shows, and policy violations. Authorized roles must also be able to apply justified manual adjustments when business policy requires it.

### BR008: Reporting and Analytics

FairSpot must provide reports for HR, facility managers, and leadership. Reports should cover demand, utilization, allocation rates, rejected requests, cancellations, no-shows, repeated exceptions, and fairness indicators.

### BR009: Role-Based Access

FairSpot must separate employee, manager, administrator, support, audit, and finance responsibilities. Users should only access the actions and data required for their role.

### BR010: Auditability and Compliance

FairSpot must keep an audit trail of important business actions, including request creation, allocation decisions, cancellations, manual overrides, penalty changes, and configuration changes.

### BR011: Multi-Tenant Customer Model

FairSpot must support multiple customer organizations. Each customer must have isolated data, configurable policies, independent users, and tenant-specific billing or subscription settings where applicable.

### BR012: Scalability and Flexibility

FairSpot must support customer growth in users, locations, parking spaces, and request volume. The product should adapt to different company parking policies without requiring custom development for every customer.

### BR013: HR Parking Request Support

FairSpot must give authorized HR and facilities users a safe operational support view for parking requests. HR must be able to answer what happened to an employee's request using business-readable data: employee display value or safe request reference, parking day, time slot, location, status or result, allocated space when present, employee-safe reason, last update, notification state where available, and permitted next action.

The support view should help HR find requests by date, employee display value or safe request reference, status, location, reason, and attention category. Attention categories should include failed or delayed Draws, rejected requests grouped by safe reason, requests needing manual follow-up, unusual cancellation or no-show patterns, and capacity mismatches.

HR support views must not expose hidden Draw seeds, candidate order, hidden weights, raw penalties, stack traces, unrelated employee records, or raw technical diagnostics. Privileged actions such as cancellation or manual correction require authorization, reason capture, employee notification when affected, and audit evidence.

### BR014: Parking Map and Capacity Visibility

FairSpot must provide role-safe visibility into parking capacity. HR, facilities, and administrators must be able to see total active and inactive spaces, general availability, company-car-only capacity, reserved capacity, EV charging, accessibility, motorcycle capability, zones, floors, and current or selected-day allocation state where available.

Employees should see a simplified map and capability view that helps them understand available capacity and constraints without exposing reserved-for user IDs, requestor references, booking IDs, or other employees' private data. The map should be based on the tenant's published resource map, slot configuration, zones, and capabilities, and it should tolerate unknown slot ID formats by falling back to configured labels.

## Business Process Summary

1. Employees submit parking requests for future or current time slots.
2. FairSpot validates eligibility, duplicate requests, time slot availability, vehicle constraints, and local policy.
3. The allocation process assigns available spaces using the configured fairness rules, zone preferences, team defaults, and fallback policy.
4. Employees receive allocation results and reminders.
5. Employees cancel or confirm usage when needed.
6. Released or unused spaces are reallocated according to policy.
7. HR and management review reports, exceptions, and usage patterns.

The full allocation description is documented in [Slot Allocation Process](./process).

## Scope Boundaries

### In Scope

- Employee parking requests and status tracking.
- Fair allocation of limited parking capacity.
- Time-slot, location, zone, space, and vehicle constraints.
- Resource maps for spaces, zones, capacity pools, and resource capabilities.
- Role-safe parking map and capacity visibility.
- Company-car, accessibility, EV, and configured slot-capability policy support.
- Notifications, cancellations, reallocations, and usage confirmation.
- Reporting, audit trail, HR request support, and role-based access.
- Customer tenant configuration.

### Future Opportunities

- Sustainability incentives for carpooling, cycling, EVs, or public transport.
- Motorcycle-specific capacity and shared motorcycle-space rules.
- Recurring reserved-space release automation.
- Advanced demand prediction and allocation optimization.
- Paid parking, subscription models, or internal cost recovery.
- Integration with building access systems, license plate recognition, or workplace calendars.

These opportunities should be treated as product extensions, not prerequisites for the core FairSpot value proposition.
