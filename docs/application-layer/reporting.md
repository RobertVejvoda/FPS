# Reporting Application

> Status: legacy transitional component. New durable cross-service read models should be designed under [DataHub](./datahub.md), not added to Reporting by default.

Reporting provides a predefined set of parking operations reports. It is not a custom report builder and should not own the durable projection database. Application behavior is centered on named report definitions, safe filter/configuration rules, manager-safe query endpoints, and deterministic exports that can be consumed by the web app or downloaded for client review.

Long-term, Reporting should be thin: it may define report names, allowed filters, export formats, column policies, and role-specific presentation rules. The underlying read data should come from DataHub projections.

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

Reporting does not decide Booking state and must not replace Audit. It should also not become the owner of CQRS read-model storage, event inbox processing, projection rebuilds, or PostgreSQL persistence. DataHub is the target component for cross-service read models and PostgreSQL-backed projections.

Out of scope for the reporting application:

- durable projection database ownership;
- event inbox, projection handlers, and rebuild/backfill processing;
- raw audit payloads, retention, integrity, and GDPR erasure evidence;
- billing, invoice, payment, or revenue reports;
- infrastructure health, backup, incident, and telemetry reports;
- broad login/session monitoring;
- custom report designers and scheduled report delivery.
