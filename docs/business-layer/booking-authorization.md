---
title: Booking Authorization
---

## Purpose

This document defines who may perform Booking actions. It is the Booking-specific RBAC/ABAC contract for the vertical slices in [Booking Implementation Slices](../implementation/booking-vertical-slices).

Authorization must use tenant and actor identity from the authenticated context. Request bodies must not be trusted for tenant ID, actor ID, role, or ownership.

## Role Names

Implementation may map these business roles to technical claims such as `employee`, `hr_manager`, `admin`, and `auditor`, but the behavior must match this table.

| Business role | Typical technical role | Meaning |
| --- | --- | --- |
| Booking Requestor | `employee` | Employee acting on their own requests. |
| Booking Adjuster | `hr_manager` | HR/facility role acting on employee bookings. |
| Booking Processor | `hr_manager` or `system` | Role or scheduled process that runs allocation workflows. |
| Configuration Manager | `admin` | Tenant administrator for policy/configuration. |
| Auditor | `auditor` | Read-only audit/review role. |
| System Actor | `system` | Scheduled workflow, retry, or integration process. |

## Authorization Matrix

| Action | Employee/requestor | HR/facility manager | Admin | Auditor | System |
| --- | --- | --- | --- | --- | --- |
| Submit future booking request | Own request only | On behalf of employee when policy allows | No by default | No | No |
| Submit same-day booking request | Own request only | On behalf of employee when policy allows | No by default | No | No |
| View own bookings | Own bookings only | Employee bookings within tenant scope | Tenant scope when support policy allows | Read-only tenant scope | No |
| Cancel pending request | Own pending request | Any tenant request with reason | No by default | No | No |
| Cancel allocated reservation | Own allocated reservation when policy allows | Any tenant allocation with reason | No by default | No | No |
| Trigger scheduled Draw manually | No | Yes, with reason | Yes, with reason | No | Scheduled trigger only |
| Run scheduled Draw workflow | No | No direct user action | No direct user action | No | Yes |
| Confirm own usage | Own allocated request when self-confirmation enabled | Yes, with source/reason | No by default | No | Integration source when configured |
| Mark no-show manually | No | Yes, with reason | Yes, with reason | No | Scheduled evaluation only |
| View Draw status | Own outcome only if employee view is exposed | Tenant/location scope | Tenant/location scope | Tenant/location read-only scope | No |
| Manual correction | No | Yes, with reason | Yes, with reason | Read-only review | No |
| View audit details | No | Limited tenant operational audit when policy allows | Tenant admin audit when policy allows | Tenant audit scope | No |

## Ownership Rules

Employee self-service actions apply only to the authenticated employee's own booking requests.

Rules:

- Employees must not act on another employee's request by passing a different requestor ID.
- Employees may only see their own booking details and employee-visible reasons.
- Employees must not see lottery seed, candidate order, weights, penalties, other employees, or audit-only diagnostics.
- HR/facility managers may act only within their tenant and assigned locations where location scoping exists.
- Auditors are read-only unless they also hold an operational role.

## Reason Requirements

These actions require a human-readable reason when performed by HR/admin instead of the employee or system:

- submitting or cancelling on behalf of an employee;
- manually triggering a Draw outside the normal schedule;
- cancelling an allocated reservation on behalf of an employee;
- manual no-show evaluation;
- manual correction;
- penalty adjustment.

The reason must be included in audit records and relevant manual-correction events.

## System Actor Rules

System actors may run scheduled jobs and workflow retries only for documented processes.

Allowed system actions:

- scheduled Draw workflow;
- workflow retry/idempotency recovery;
- scheduled no-show evaluation;
- scheduled expiry of stale pending requests;
- notification retry;
- read-model projection.

System actors must still emit audit/event records with a stable system actor identity.

## Denial Behavior

Unauthorized actions must be rejected without leaking private details.

Rules:

- Return a generic authorization failure for missing role/scope.
- Do not reveal whether another employee's booking exists.
- Do not include audit-only diagnostics in authorization errors.
- Record authorization failures in security/audit logs when appropriate.

## Slice Notes

- B001/B002: tenant and requestor identity come from authenticated context unless HR is explicitly acting on behalf of an employee.
- B003/B005: employee cancellation requires ownership; HR/admin cancellation requires reason.
- B004: manual Draw trigger requires HR/admin and reason; scheduled trigger uses system actor.
- B006: employee self-confirmation requires ownership and enabled self-confirmation policy; HR/manual confirmation requires authorized role.
- B007: manual no-show trigger requires HR/admin and reason; scheduled evaluation uses system actor.
- B008: employee view is own bookings only.
- B009: HR/admin/auditor view may include operational summaries; employee view is own outcome only if exposed.
- B010: corrections require HR/admin and reason; auditors review but do not correct.
