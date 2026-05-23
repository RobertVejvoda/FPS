# HR Bootstrap Import Contract

FPS uses a lightweight CSV import to bootstrap demo and pilot tenants without requiring first names, last names, employee IDs, or passwords. This document defines the contract between HR/IT and the FPS platform.

## Ownership boundaries

| Data | Owner | Stored in FPS |
|------|-------|---------------|
| Identity (username, password, MFA) | Company IdP | No |
| External subject / username | Company IdP | As a link key only |
| Display name | HR (optional) | Yes, UI only |
| Email | HR (optional) | Yes, notifications only |
| Roles | HR / IT admin | Yes |
| Location, zone, eligibility flags | HR | Yes |
| Vehicle license plates | HR / employee | Yes |

FPS never requests or stores: passwords, national IDs, employee numbers, salary, date of birth, or personal addresses.

## File format

Two CSV files are used: one for employees, one for vehicles. Both use comma separators and support comment lines starting with `#`.

### employees.csv

| Column | Required | Values | Notes |
|--------|----------|--------|-------|
| `external_subject` | Yes | string | Stable IdP identifier (username or `sub` claim). Must not change after import. |
| `display_name` | No | string | UI display only. |
| `email` | No | string | Notifications only. |
| `roles` | Yes | see below | Semicolon-separated. |
| `home_location` | Yes | location code | Must match a configured FPS location. |
| `preferred_zone` | No | zone code | Leave blank for no preference. |
| `parking_eligible` | Yes | `true`/`false` | |
| `has_company_car` | Yes | `true`/`false` | |
| `accessibility_eligible` | Yes | `true`/`false` | |
| `reserved_space_eligible` | Yes | `true`/`false` | |
| `active` | Yes | `true`/`false` | `false` disables the account. |

### vehicles.csv

| Column | Required | Values | Notes |
|--------|----------|--------|-------|
| `external_subject` | Yes | string | Must match a subject in employees.csv. |
| `vehicle_alias` | No | string | Friendly name shown in UI. |
| `vehicle_license_plate` | Yes | string | Must be unique across all vehicles. |
| `vehicle_type` | Yes | `car`, `motorcycle`, `van` | |
| `vehicle_is_electric` | Yes | `true`/`false` | Used for EV space allocation. |
| `active` | Yes | `true`/`false` | |

One user may have zero, one, or multiple vehicle rows.

## Valid roles

| Role | Access |
|------|--------|
| `employee` | Bookings, profile, notifications |
| `hr_manager` | Reports, configuration |
| `admin` | Tenant admin, reports, configuration, audit |
| `report_viewer` | Reports only |
| `auditor` | Audit log only |

Multiple roles can be assigned by separating with a semicolon: `employee;hr_manager`.

## Validation rules

The `tools/validate-hr-import.sh` script enforces:

- No forbidden columns (`password`, `passwd`, `secret`, `token`, `credential`, `ssn`, `national_id`, `salary`)
- All required columns present
- No duplicate `external_subject` values in employees.csv
- No duplicate `vehicle_license_plate` values in vehicles.csv
- Roles must be from the valid set above
- Boolean fields must be exactly `true` or `false`
- Vehicle `external_subject` must reference a known employee (when both files are provided)
- `vehicle_type` must be one of `car`, `motorcycle`, `van`

Run validation before importing:

```bash
./tools/validate-hr-import.sh tools/templates/employees.csv tools/templates/vehicles.csv
```

## Templates

Blank templates with column documentation are in `tools/templates/`:

- `employees.csv` — employee template
- `vehicles.csv` — vehicle template
- `demo-employees.csv` — local demo tenant sample (7 users)
- `demo-vehicles.csv` — local demo tenant vehicles

## What stays in the IdP

The company IdP remains authoritative for:
- Password and MFA
- Group membership (mapped to FPS roles via tenant role mapping config)
- Account lockout and suspension

FPS only stores the minimum needed for access control, allocation, and demo usability. When a user is removed from the IdP, their FPS account can be deactivated by setting `active=false` and re-importing.
