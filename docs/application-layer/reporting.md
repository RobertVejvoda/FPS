# Reporting Application

Reporting provides a predefined set of parking operations reports. It is not a custom report builder. Application behavior is centered on tenant-scoped read models, manager-safe query endpoints, and deterministic exports that can be consumed by the web app or downloaded for client review.

## Application Functions

### Parking Summary

- Query tenant-scoped daily parking request, allocation, rejection, cancellation, confirmation, and no-show counts.
- Group results by date, location, and safe report filters.
- Return empty report responses instead of errors when no data exists for the selected range.

### Fairness Reporting

- Show allocation fairness over time using pseudonymised or aggregated metrics.
- Avoid hidden lottery internals such as seeds, raw ordering, internal weights, or diagnostics that would undermine the fairness model.
- Support manager review without exposing another employee's private details unless role policy explicitly permits it.

### Utilization Reporting

- Report capacity and allocation utilization by location, date/time, and configured slot or capability category.
- Help HR/facilities identify under-used or overloaded capacity.
- Keep slot metadata safe for the manager role and avoid exposing reserved-space details that are not operationally necessary.

### Reason-Code Reporting

- Aggregate rejection, cancellation, no-show, and expiry reasons.
- Use documented reason codes and employee-safe reason groups.
- Support root-cause review without exposing internal algorithm diagnostics.

### Export

- Export approved operational reports as deterministic CSV.
- Preserve tenant isolation and privacy-safe shaping in export output.
- Neutralize spreadsheet formula injection.

## Boundaries

Reporting does not decide Booking state and must not replace Audit.

Out of scope for the reporting application:

- raw audit payloads, retention, integrity, and GDPR erasure evidence;
- billing, invoice, payment, or revenue reports;
- infrastructure health, backup, incident, and telemetry reports;
- broad login/session monitoring;
- custom report designers and scheduled report delivery.
