---
title: Business Requirements
---

## Business Context

Many companies have fewer parking spaces than employees who want to use them. When parking is managed by email, spreadsheets, or informal agreements, HR becomes the bottleneck and employees perceive the process as arbitrary. First-come, first-served allocation is simple, but it rewards speed and insider knowledge rather than actual business need or fair access.

Fair Parking System (FPS) replaces manual parking coordination with a transparent, automated allocation process. Employees request parking for specific time slots, the system allocates available capacity using configurable fairness rules, and all affected users receive clear status updates. The customer value is reduced administration, higher employee trust, better parking utilization, and auditable policy enforcement.

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
   - Respect reserved spaces, company-car rules, accessibility needs, motorcycle parking, EV charging, and other local constraints.
   - Allow each customer tenant to configure rules without changing the product.

5. **Improve employee experience**
   - Let employees request, cancel, and track parking without contacting HR.
   - Notify users when requests are submitted, allocated, rejected, cancelled, or reallocated.
   - Integrate with calendars and reminders where it reduces missed reservations.

6. **Provide management insight**
   - Report on demand, allocation fairness, utilization, cancellations, no-shows, and policy exceptions.
   - Help HR and management make evidence-based decisions about parking capacity and commuting policy.

## Core Business Requirements

### BR001: Automated Parking Requests

Employees must be able to submit parking requests through FPS instead of email. A request must identify the employee, vehicle where needed, preferred date, time slot, location, and relevant parking requirements.

### BR002: Fair Slot Allocation

FPS must allocate limited parking capacity using transparent and configurable fairness rules. The process must avoid persistent favoritism and should improve the chance of allocation for employees who have received fewer recent spaces.

### BR003: Configurable Parking Policies

Customers must be able to configure parking rules for their organization, including locations, spaces, time slots, eligibility, reserved-space policies, company-car handling, motorcycles, EV charging, accessibility needs, and penalties.

### BR004: Real-Time Status and Notifications

Employees must receive clear status updates for request submission, allocation outcome, cancellation, reallocation, and reminders. Notifications should be available in the application and may also be delivered by email, push notification, or calendar integration.

### BR005: Cancellation and Reallocation

Employees must be able to cancel requests and reservations. If a confirmed space becomes available, FPS should offer or allocate it to another eligible employee according to customer policy.

### BR006: Usage Confirmation

FPS should support confirmation of actual parking usage, either by user action, access-control integration, QR code, card reader, or another customer-specific signal. Usage data is required for fairness, penalties, and reporting.

### BR007: Penalties and Adjustments

FPS must support configurable penalties for late cancellations, no-shows, and policy violations. Authorized roles must also be able to apply justified manual adjustments when business policy requires it.

### BR008: Reporting and Analytics

FPS must provide reports for HR, facility managers, and leadership. Reports should cover demand, utilization, allocation rates, rejected requests, cancellations, no-shows, repeated exceptions, and fairness indicators.

### BR009: Role-Based Access

FPS must separate employee, manager, administrator, support, audit, and finance responsibilities. Users should only access the actions and data required for their role.

### BR010: Auditability and Compliance

FPS must keep an audit trail of important business actions, including request creation, allocation decisions, cancellations, manual overrides, penalty changes, and configuration changes.

### BR011: Multi-Tenant Customer Model

FPS must support multiple customer organizations. Each customer must have isolated data, configurable policies, independent users, and tenant-specific billing or subscription settings where applicable.

### BR012: Scalability and Flexibility

FPS must support customer growth in users, locations, parking spaces, and request volume. The product should adapt to different company parking policies without requiring custom development for every customer.

## Business Process Summary

1. Employees submit parking requests for future or current time slots.
2. FPS validates eligibility, duplicate requests, time slot availability, vehicle constraints, and local policy.
3. The allocation process assigns available spaces using the configured fairness rules.
4. Employees receive allocation results and reminders.
5. Employees cancel or confirm usage when needed.
6. Released or unused spaces are reallocated according to policy.
7. HR and management review reports, exceptions, and usage patterns.

The full allocation description is documented in [Slot Allocation Process](./process).

## Scope Boundaries

### In Scope

- Employee parking requests and status tracking.
- Fair allocation of limited parking capacity.
- Time-slot, location, space, and vehicle constraints.
- Reserved-space and company-car policy support.
- Notifications, cancellations, reallocations, and usage confirmation.
- Reporting, audit trail, and role-based access.
- Customer tenant configuration.

### Future Opportunities

- Sustainability incentives for carpooling, cycling, EVs, or public transport.
- Advanced demand prediction and allocation optimization.
- Paid parking, subscription models, or internal cost recovery.
- Integration with building access systems, license plate recognition, or workplace calendars.

These opportunities should be treated as product extensions, not prerequisites for the core FPS value proposition.
