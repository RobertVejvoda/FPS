#!/usr/bin/env bash
# Validates HR import CSV files against the FPS bootstrap contract.
# Usage: ./tools/validate-hr-import.sh <employees.csv> [vehicles.csv]
set -euo pipefail

EMPLOYEES_FILE="${1:-}"
VEHICLES_FILE="${2:-}"
ERRORS=0

RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

error() { echo -e "${RED}ERROR: $1${NC}" >&2; ERRORS=$((ERRORS + 1)); }
ok()    { echo -e "${GREEN}OK:${NC} $1"; }

# Exact allowed column sets — any column not in this list is rejected.
EMPLOYEE_COLS="external_subject,display_name,email,roles,home_location,preferred_zone,parking_eligible,has_company_car,accessibility_eligible,reserved_space_eligible,active"
VEHICLE_COLS="external_subject,vehicle_alias,vehicle_license_plate,vehicle_type,vehicle_is_electric,active"

VALID_ROLES="employee hr_manager admin report_viewer auditor"
FORBIDDEN_COLS="password passwd secret token credential ssn national_id salary employee_id manager_notes department internal_role"
VALID_VEHICLE_TYPES="car motorcycle van"

# Local demo allowed values. Production tenants extend these via tenant config.
ALLOWED_LOCATIONS="Prague"
ALLOWED_ZONES="A B COVERED"

validate_employees() {
  local file="$1"
  echo "=== Validating employees: $file ==="

  if [[ ! -f "$file" ]]; then
    error "File not found: $file"
    return
  fi

  local header
  header=$(grep -v '^#' "$file" | head -1)

  # Check forbidden columns.
  for col in $FORBIDDEN_COLS; do
    if echo "$header" | grep -qi "$col"; then
      error "Forbidden column '$col' found — do not include secrets or personal data"
    fi
  done

  # Reject unknown columns: every header column must be in the allowed set.
  IFS=',' read -ra header_cols <<< "$header"
  IFS=',' read -ra allowed_cols <<< "$EMPLOYEE_COLS"
  for col in "${header_cols[@]}"; do
    col=$(echo "$col" | tr -d '[:space:]')
    found=false
    for allowed in "${allowed_cols[@]}"; do
      [[ "$col" == "$allowed" ]] && found=true && break
    done
    if [[ "$found" == false ]]; then
      error "Unknown column '$col' — only these columns are allowed: $EMPLOYEE_COLS"
    fi
  done

  # Check required columns are present.
  for col in external_subject roles home_location parking_eligible has_company_car accessibility_eligible reserved_space_eligible active; do
    if ! echo "$header" | grep -q "$col"; then
      error "Missing required column '$col'"
    fi
  done

  local subjects=()
  local line_no=1
  while IFS=',' read -r subject _display _email roles home_location preferred_zone parking company_car access reserved active; do
    line_no=$((line_no + 1))
    [[ "$subject" == external_subject ]] && continue
    [[ "$subject" =~ ^# ]] && continue
    [[ -z "$subject" ]] && continue

    # Duplicate check.
    if [[ ${#subjects[@]} -gt 0 ]] && printf '%s\n' "${subjects[@]}" | grep -qx "$subject"; then
      error "Line $line_no: duplicate external_subject '$subject'"
    fi
    subjects+=("$subject")

    # Roles: semicolon-separated, each must be in the valid set.
    if [[ -n "$roles" ]]; then
      IFS=';' read -ra role_list <<< "$roles"
      for role in "${role_list[@]}"; do
        role=$(echo "$role" | tr -d ' ')
        if ! echo "$VALID_ROLES" | grep -qw "$role"; then
          error "Line $line_no: unknown role '$role' for '$subject' — valid: $VALID_ROLES"
        fi
      done
    else
      error "Line $line_no: roles is required for '$subject'"
    fi

    # Location: must be in the allowed set.
    home_location=$(echo "$home_location" | tr -d ' \r')
    if [[ -n "$home_location" ]]; then
      if ! echo "$ALLOWED_LOCATIONS" | grep -qw "$home_location"; then
        error "Line $line_no: unknown home_location '$home_location' for '$subject' — allowed: $ALLOWED_LOCATIONS"
      fi
    else
      error "Line $line_no: home_location is required for '$subject'"
    fi

    # Zone: optional, but if provided must be in the allowed set.
    preferred_zone=$(echo "$preferred_zone" | tr -d ' \r')
    if [[ -n "$preferred_zone" ]] && ! echo "$ALLOWED_ZONES" | grep -qw "$preferred_zone"; then
      error "Line $line_no: unknown preferred_zone '$preferred_zone' for '$subject' — allowed: $ALLOWED_ZONES"
    fi

    # Boolean fields.
    for field_val in "$parking" "$company_car" "$access" "$reserved" "$active"; do
      field_val=$(echo "$field_val" | tr -d ' \r')
      if [[ -n "$field_val" && ! "$field_val" =~ ^(true|false)$ ]]; then
        error "Line $line_no: boolean field has non-boolean value '$field_val' for '$subject'"
      fi
    done

  done < <(grep -v '^#' "$file")

  ok "Employees validated (${#subjects[@]} records)"
}

validate_vehicles() {
  local file="$1"
  local subjects_file="$2"
  echo "=== Validating vehicles: $file ==="

  if [[ ! -f "$file" ]]; then
    error "File not found: $file"
    return
  fi

  local header
  header=$(grep -v '^#' "$file" | head -1)

  # Check forbidden columns.
  for col in $FORBIDDEN_COLS; do
    if echo "$header" | grep -qi "$col"; then
      error "Forbidden column '$col' found"
    fi
  done

  # Reject unknown columns.
  IFS=',' read -ra header_cols <<< "$header"
  IFS=',' read -ra allowed_cols <<< "$VEHICLE_COLS"
  for col in "${header_cols[@]}"; do
    col=$(echo "$col" | tr -d '[:space:]')
    found=false
    for allowed in "${allowed_cols[@]}"; do
      [[ "$col" == "$allowed" ]] && found=true && break
    done
    if [[ "$found" == false ]]; then
      error "Unknown column '$col' — only these columns are allowed: $VEHICLE_COLS"
    fi
  done

  # Check required columns.
  for col in external_subject vehicle_license_plate vehicle_type vehicle_is_electric active; do
    if ! echo "$header" | grep -q "$col"; then
      error "Missing required column '$col'"
    fi
  done

  local known_subjects=()
  if [[ -f "$subjects_file" ]]; then
    while IFS=',' read -r subject _rest; do
      [[ "$subject" == external_subject || "$subject" =~ ^# || -z "$subject" ]] && continue
      known_subjects+=("$subject")
    done < <(grep -v '^#' "$subjects_file")
  fi

  local plates=()
  local line_no=1
  while IFS=',' read -r subject _alias plate vtype electric active; do
    line_no=$((line_no + 1))
    [[ "$subject" == external_subject ]] && continue
    [[ "$subject" =~ ^# ]] && continue
    [[ -z "$subject" ]] && continue

    # Subject must exist in employees file.
    if [[ ${#known_subjects[@]} -gt 0 ]]; then
      if ! printf '%s\n' "${known_subjects[@]}" | grep -qx "$subject"; then
        error "Line $line_no: vehicle references unknown external_subject '$subject'"
      fi
    fi

    # Required plate.
    plate=$(echo "$plate" | tr -d ' \r')
    [[ -z "$plate" ]] && error "Line $line_no: vehicle_license_plate is required for '$subject'"

    # Duplicate plate.
    if [[ ${#plates[@]} -gt 0 ]] && printf '%s\n' "${plates[@]}" | grep -qx "$plate"; then
      error "Line $line_no: duplicate vehicle_license_plate '$plate'"
    fi
    [[ -n "$plate" ]] && plates+=("$plate")

    # Vehicle type.
    vtype=$(echo "$vtype" | tr -d ' \r')
    if [[ -n "$vtype" ]] && ! echo "$VALID_VEHICLE_TYPES" | grep -qw "$vtype"; then
      error "Line $line_no: unknown vehicle_type '$vtype' — valid: $VALID_VEHICLE_TYPES"
    fi

    # Boolean fields.
    for field_val in "$electric" "$active"; do
      field_val=$(echo "$field_val" | tr -d ' \r')
      if [[ -n "$field_val" && ! "$field_val" =~ ^(true|false)$ ]]; then
        error "Line $line_no: boolean field has non-boolean value '$field_val'"
      fi
    done

  done < <(grep -v '^#' "$file")

  ok "Vehicles validated (${#plates[@]} records)"
}

if [[ -z "$EMPLOYEES_FILE" ]]; then
  echo "Usage: $0 <employees.csv> [vehicles.csv]"
  exit 1
fi

validate_employees "$EMPLOYEES_FILE"

if [[ -n "$VEHICLES_FILE" ]]; then
  validate_vehicles "$VEHICLES_FILE" "$EMPLOYEES_FILE"
fi

if [[ $ERRORS -gt 0 ]]; then
  echo ""
  echo -e "${RED}Validation failed with $ERRORS error(s).${NC}"
  exit 1
else
  echo ""
  echo -e "${GREEN}Validation passed.${NC}"
fi
