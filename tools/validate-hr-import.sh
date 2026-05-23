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

EMPLOYEE_COLS="external_subject,display_name,email,roles,home_location,preferred_zone,parking_eligible,has_company_car,accessibility_eligible,reserved_space_eligible,active"
VEHICLE_COLS="external_subject,vehicle_alias,vehicle_license_plate,vehicle_type,vehicle_is_electric,active"
VALID_ROLES="employee hr_manager admin report_viewer auditor"
FORBIDDEN_COLS="password passwd secret token credential ssn national_id salary"
VALID_VEHICLE_TYPES="car motorcycle van"

validate_employees() {
  local file="$1"
  echo "=== Validating employees: $file ==="

  if [[ ! -f "$file" ]]; then
    error "File not found: $file"
    return
  fi

  local header
  header=$(grep -v '^#' "$file" | head -1)

  # Check forbidden columns
  for col in $FORBIDDEN_COLS; do
    if echo "$header" | grep -qi "$col"; then
      error "Forbidden column '$col' found in $file — do not include secrets or personal data"
    fi
  done

  # Check required columns
  IFS=',' read -ra required <<< "$EMPLOYEE_COLS"
  for col in "${required[@]}"; do
    if ! echo "$header" | grep -q "$col"; then
      error "Missing required column '$col' in $file"
    fi
  done

  local subjects=()
  local line_no=1
  while IFS=',' read -r subject _display _email roles _home _zone parking company_car access reserved active; do
    line_no=$((line_no + 1))
    [[ "$subject" == external_subject ]] && continue
    [[ "$subject" =~ ^# ]] && continue
    [[ -z "$subject" ]] && continue

    # Duplicate check
    if [[ ${#subjects[@]} -gt 0 ]] && printf '%s\n' "${subjects[@]}" | grep -qx "$subject"; then
      error "Line $line_no: duplicate external_subject '$subject'"
    fi
    subjects+=("$subject")

    # Required field
    [[ -z "$subject" ]] && error "Line $line_no: external_subject is required"

    # Roles validation
    if [[ -n "$roles" ]]; then
      IFS=';' read -ra role_list <<< "$roles"
      for role in "${role_list[@]}"; do
        role=$(echo "$role" | tr -d ' ')
        if ! echo "$VALID_ROLES" | grep -qw "$role"; then
          error "Line $line_no: unknown role '$role' for subject '$subject' — valid: $VALID_ROLES"
        fi
      done
    fi

    # Boolean fields
    for field_val in "$parking" "$company_car" "$access" "$reserved" "$active"; do
      if [[ -n "$field_val" && ! "$field_val" =~ ^(true|false)$ ]]; then
        error "Line $line_no: boolean field has non-boolean value '$field_val' for subject '$subject'"
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

  for col in $FORBIDDEN_COLS; do
    if echo "$header" | grep -qi "$col"; then
      error "Forbidden column '$col' found in $file"
    fi
  done

  IFS=',' read -ra required <<< "$VEHICLE_COLS"
  for col in "${required[@]}"; do
    if ! echo "$header" | grep -q "$col"; then
      error "Missing required column '$col' in $file"
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

    # Subject must exist in employees file (if provided)
    if [[ ${#known_subjects[@]} -gt 0 ]]; then
      if ! printf '%s\n' "${known_subjects[@]}" | grep -qx "$subject"; then
        error "Line $line_no: vehicle references unknown external_subject '$subject'"
      fi
    fi

    # Required plate
    [[ -z "$plate" ]] && error "Line $line_no: vehicle_license_plate is required for subject '$subject'"

    # Duplicate plate
    if [[ ${#plates[@]} -gt 0 ]] && printf '%s\n' "${plates[@]}" | grep -qx "$plate"; then
      error "Line $line_no: duplicate vehicle_license_plate '$plate'"
    fi
    [[ -n "$plate" ]] && plates+=("$plate")

    # Vehicle type
    if [[ -n "$vtype" ]] && ! echo "$VALID_VEHICLE_TYPES" | grep -qw "$vtype"; then
      error "Line $line_no: unknown vehicle_type '$vtype' — valid: $VALID_VEHICLE_TYPES"
    fi

    # Boolean fields
    for field_val in "$electric" "$active"; do
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
