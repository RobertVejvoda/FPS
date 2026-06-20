# HR Bootstrap Import Contract

FairSpot uses a lightweight CSV import to bootstrap demo and pilot tenants without requiring first names, last names, employee IDs, or passwords. This document defines the contract between HR/IT and FairSpot.

## Ownership boundaries

| Data | Owner | Stored in FairSpot |
|------|-------|---------------|
| Identity (username, password, MFA) | Company IdP | No |
| External subject / username | Company IdP | As a link key only |
| Display name | HR (optional) | Yes, UI only |
| Email | HR (optional) | Yes, notifications only |
| Roles | HR / IT admin | Yes |
| Location, zone, eligibility flags | HR | Yes |
| Vehicle license plates | HR / employee | Yes |

FairSpot never requests or stores: passwords, national IDs, employee numbers, salary, date of birth, or personal addresses.

## File format

Both files use comma separators and support comment lines starting with `#`.

The web HR Import page supports both `employees.csv` (required) and `vehicles.csv` (optional) in a single
preview-and-commit flow.

### employees.csv

| Column | Required | Values | Notes |
|--------|----------|--------|-------|
| `external_subject` | Yes | string | Stable IdP identifier (username or `sub` claim). Must not change after import. |
| `display_name` | No | string | UI display only. |
| `email` | No | string | Notifications only. |
| `roles` | Yes | see below | Semicolon-separated. |
| `home_location` | Yes | location code | Must match a configured FairSpot location. |
| `preferred_zone` | No | zone code | Leave blank for no preference. |
| `parking_eligible` | Yes | `true`/`false` | |
| `has_company_car` | Yes | `true`/`false` | |
| `accessibility_eligible` | Yes | `true`/`false` | |
| `reserved_space_eligible` | Yes | `true`/`false` | |
| `active` | Yes | `true`/`false` | `false` disables the account. |

Exact employee header:

```csv
external_subject,display_name,email,roles,home_location,preferred_zone,parking_eligible,has_company_car,accessibility_eligible,reserved_space_eligible,active
```

Minimal employee example:

```csv
external_subject,display_name,email,roles,home_location,preferred_zone,parking_eligible,has_company_car,accessibility_eligible,reserved_space_eligible,active
employee1,Jan Novak,jan.novak@example.invalid,employee,Prague,A,true,false,false,false,true
employee2,Petra Svobodova,petra.svobodova@example.invalid,employee,Prague,,true,true,false,false,true
hr-admin,Lucie Prochazkova,lucie.prochazkova@example.invalid,employee;hr_manager,Prague,,false,false,false,false,true
```

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

Exact vehicle header:

```csv
external_subject,vehicle_alias,vehicle_license_plate,vehicle_type,vehicle_is_electric,active
```

Minimal vehicle example:

```csv
external_subject,vehicle_alias,vehicle_license_plate,vehicle_type,vehicle_is_electric,active
employee1,Daily Driver,1AA 2345,car,false,true
employee1,EV Commuter,2AB 3456,car,true,true
employee2,Company Fleet,3AC 4567,car,false,true
```

Company-car note: `has_company_car` is an HR/fleet-controlled employee/profile fact. Employees must not be able to mark themselves as company-car users through self-service. Fixed company-car slot assignment is controlled separately by HR/facilities capacity configuration.

## Valid roles

| Role | Access |
|------|--------|
| `employee` | Bookings, profile, notifications |
| `hr_manager` | Reports, configuration |
| `admin` | Tenant admin, reports, configuration, audit |
| `report_viewer` | Reports only |
| `auditor` | Audit log only |

Multiple roles must be separated with a semicolon: `employee;hr_manager`. Commas are not supported as role separators (they are the CSV field delimiter).

## Validation rules

The `tools/validate-hr-import.sh` script enforces:

- No forbidden columns (`password`, `passwd`, `secret`, `token`, `credential`, `ssn`, `national_id`, `salary`, `employee_id`, `manager_notes`, `department`, `internal_role`)
- No unknown columns — only the documented column set is accepted; any additional column is an error
- All required columns present
- `home_location` must match a configured FairSpot location (local demo: `Prague`)
- `preferred_zone` must be one of the allowed zones when provided (local demo: `A`, `B`, `COVERED`)
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
- Group membership (mapped to FairSpot roles via tenant role mapping config)
- Account lockout and suspension

FairSpot only stores the minimum needed for access control, allocation, and demo usability. When a user is removed from the IdP, their FairSpot account can be deactivated by setting `active=false` and re-importing.
