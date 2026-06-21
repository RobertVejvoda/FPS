namespace FPS.SharedKernel.Capacity;

// Issue #533: shared helper for the company-car fixed-slot capacity warning.
// The slot-side semantics intentionally mirror CompanyCarReservedSlotRules.Resolve
// in Booking (FPS.Booking/Domain/Services/CompanyCarReservedSlotRules.cs). The
// allocator immediately allocates an on-time company-car request when there is
// any active slot reserved for that requestor that the vehicle can use
// (AvailableSlot.CanAccommodate). A slot reserved for a user is therefore the
// guarantee, not the IsCompanyCarOnly flag.
//
// The compatibility gates AvailableSlot.CanAccommodate applies are:
//   * IsActive
//   * (IsCompanyCarReserved && !vehicle.IsCompanyCar) → company-car-only slot rejects
//     non-company-car vehicles. Not a barrier for a company-car employee.
//   * (IsMotorcycleCapacity && vehicle.Type != Motorcycle) → motorcycle bay
//     rejects company cars. The warning excludes these as guarantees.
//   * (vehicle.IsElectric && !HasCharger) → a charger is required when the
//     vehicle is electric. The warning mirrors this by treating an employee
//     as needing a charger only when ALL of their active vehicles are
//     electric (any ICE option lets them request without charging needs).
//   * (vehicle.RequiresAccessibleSpot && !IsAccessible) → the slot must be
//     accessibility-friendly when the employee is accessibility-eligible
//     (the profile-level flag drives RequiresAccessibleSpot at request time).
//
// User id comparison mirrors AvailableSlot.IsReservedFor / NormalizeReservedForUserId:
// case-insensitive (OrdinalIgnoreCase) with surrounding whitespace trimmed.
// Otherwise the warning can show a false positive when slot reservations and
// employee user ids differ only in casing or trailing spaces, while the
// allocator would actually honor the reservation.
//
// The helper is intentionally cross-service-free: callers pass their own
// snapshot of slots (Configuration) and company-car users (Profile). That keeps
// the comparison deterministic and unit-testable without HTTP plumbing.

/// <summary>
/// A minimal projection of a parking slot needed to classify it as a
/// guaranteed company-car fixed slot. Mirrors the Configuration ParkingSlot
/// shape but stays free of any service-specific dependency.
/// </summary>
/// <param name="IsMotorcycleCapacity">
/// True when the slot is a motorcycle-only bay. Motorcycle bays reject company
/// cars at allocation time (AvailableSlot.CanAccommodate), so they do not count
/// as a guarantee for company-car employees.
/// </param>
/// <param name="HasCharger">
/// True when the slot has an EV charger. Required for an electric vehicle to
/// be allocated to this slot.
/// </param>
/// <param name="IsAccessible">
/// True when the slot is accessibility-friendly. Required for an
/// accessibility-eligible employee to be allocated to this slot.
/// </param>
public readonly record struct CompanyCarCapacitySlot(
    string LocationId,
    bool IsActive,
    bool IsMotorcycleCapacity,
    bool HasCharger,
    bool IsAccessible,
    string? ReservedForUserId);

/// <summary>
/// A minimal projection of a company-car employee with the vehicle/accessibility
/// traits the warning needs to mirror AvailableSlot.CanAccommodate.
/// </summary>
/// <param name="UserId">Tenant-scoped user id of the company-car employee.</param>
/// <param name="RequiresChargerForEveryRequest">
/// True when EVERY active vehicle on the profile is electric. In that case the
/// employee cannot avoid the charger requirement by picking a non-EV option,
/// so a non-charger slot is not a viable guarantee. False when the profile
/// has at least one non-EV active vehicle (or no vehicles at all — license-
/// plate is required at request time and the choice is up to the employee).
/// </param>
/// <param name="RequiresAccessibleSpot">
/// True when the employee is accessibility-eligible. Mirrors how Booking
/// builds VehicleInformation.RequiresAccessibleSpot from
/// snapshot.AccessibilityEligible at request time.
/// </param>
public readonly record struct CompanyCarCapacityUser(
    string UserId,
    bool RequiresChargerForEveryRequest,
    bool RequiresAccessibleSpot);

/// <summary>
/// Computed warning for a single location.
/// </summary>
/// <param name="LocationId">Configuration location id this row refers to.</param>
/// <param name="CompanyCarEmployeeCount">
/// Active company-car employees assigned to this location.
/// </param>
/// <param name="ActiveCompatibleFixedSlotCount">
/// Distinct users covered by an active fixed slot reserved for them at this
/// location whose traits are compatible with the employee's request profile
/// (no motorcycle bay; charger present when required; accessible when required).
/// Mirrors what the allocator will honor as an immediate Tier 1 allocation
/// for a company-car request.
/// </param>
/// <param name="EmployeesWithoutGuaranteedSlot">
/// Company-car employees at this location whose userId is NOT reserved on any
/// active compatible fixed slot at this location. They can still receive a slot
/// via normal allocation when policy allows, but the guarantee does not hold.
/// </param>
/// <param name="IsCapacityExceeded">
/// True when at least one company-car employee at this location has no
/// guaranteed fixed slot. The UI should surface a warning in that case.
/// </param>
public sealed record CompanyCarCapacityWarning(
    string LocationId,
    int CompanyCarEmployeeCount,
    int ActiveCompatibleFixedSlotCount,
    int EmployeesWithoutGuaranteedSlot,
    bool IsCapacityExceeded);

public static class CompanyCarCapacityCalculator
{
    // Single source of truth for user-id equality: matches
    // AvailableSlot.IsReservedFor (StringComparison.OrdinalIgnoreCase) so the
    // warning never disagrees with the allocator on whether a reservation
    // matches the requestor.
    private static readonly StringComparer UserIdComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Compute one warning row per location. A location appears in the result
    /// when it has at least one company-car employee assigned OR at least one
    /// active compatible fixed company-car slot. Locations with neither are
    /// omitted so the UI does not show noise for unrelated sites.
    /// </summary>
    /// <param name="companyCarUsersByLocation">
    /// For each location, the active company-car employees assigned to that
    /// location (e.g. by HomeLocationId) with their vehicle/accessibility traits.
    /// </param>
    /// <param name="slots">All known slots across all locations for the tenant.</param>
    public static IReadOnlyList<CompanyCarCapacityWarning> Compute(
        IReadOnlyDictionary<string, IReadOnlyList<CompanyCarCapacityUser>> companyCarUsersByLocation,
        IEnumerable<CompanyCarCapacitySlot> slots)
    {
        ArgumentNullException.ThrowIfNull(companyCarUsersByLocation);
        ArgumentNullException.ThrowIfNull(slots);

        // Group eligible reserved slots by location. We exclude motorcycle
        // bays here because they fail AvailableSlot.CanAccommodate for any
        // company-car vehicle regardless of EV / accessibility traits. The
        // remaining trait-based gates (charger, accessible) are evaluated
        // per-user further down because they depend on the employee profile.
        var slotsByLocation = slots
            .Where(s => s.IsActive
                        && !s.IsMotorcycleCapacity
                        && !string.IsNullOrWhiteSpace(s.ReservedForUserId))
            .Select(s => s with { ReservedForUserId = NormaliseUserId(s.ReservedForUserId) })
            .Where(s => !string.IsNullOrEmpty(s.ReservedForUserId))
            .GroupBy(s => s.LocationId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CompanyCarCapacitySlot>)g.ToList(),
                StringComparer.Ordinal);

        var locations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in companyCarUsersByLocation.Keys) locations.Add(k);
        foreach (var k in slotsByLocation.Keys) locations.Add(k);

        var result = new List<CompanyCarCapacityWarning>(locations.Count);
        foreach (var locationId in locations.OrderBy(l => l, StringComparer.Ordinal))
        {
            var users = companyCarUsersByLocation.TryGetValue(locationId, out var u)
                ? DistinctByUserId(u)
                : new List<CompanyCarCapacityUser>();
            var locationSlots = slotsByLocation.TryGetValue(locationId, out var s)
                ? s
                : (IReadOnlyList<CompanyCarCapacitySlot>)Array.Empty<CompanyCarCapacitySlot>();

            // A user is "guaranteed" iff there is at least one slot at the
            // location that is reserved for them AND that the allocator would
            // accept for their request profile (charger when EV-only,
            // accessible when accessibility-eligible). Set semantics dedup
            // multi-slot reservations: extra slots for the same user do not
            // cover anyone else (slots are user-specific).
            var guaranteedUsers = new HashSet<string>(UserIdComparer);
            foreach (var user in users)
            {
                foreach (var slot in locationSlots)
                {
                    if (!UserIdComparer.Equals(slot.ReservedForUserId, user.UserId))
                        continue;
                    if (user.RequiresChargerForEveryRequest && !slot.HasCharger)
                        continue;
                    if (user.RequiresAccessibleSpot && !slot.IsAccessible)
                        continue;
                    guaranteedUsers.Add(user.UserId);
                    break;
                }
            }

            // Distinct-count of reserved users at this location (whether or
            // not those reservations match an active employee here). Mirrors
            // the existing "active compatible fixed slot capacity" semantics:
            // slots reserved for unknown users still represent a configured
            // guarantee in the location, just not for anyone in `users`.
            var activeFixedSlotUserIds = new HashSet<string>(UserIdComparer);
            foreach (var slot in locationSlots)
                if (!string.IsNullOrEmpty(slot.ReservedForUserId))
                    activeFixedSlotUserIds.Add(slot.ReservedForUserId!);
            var fixedSlotCount = activeFixedSlotUserIds.Count;

            var withoutGuarantee = users.Count - guaranteedUsers.Count;

            result.Add(new CompanyCarCapacityWarning(
                LocationId: locationId,
                CompanyCarEmployeeCount: users.Count,
                ActiveCompatibleFixedSlotCount: fixedSlotCount,
                EmployeesWithoutGuaranteedSlot: withoutGuarantee,
                IsCapacityExceeded: withoutGuarantee > 0));
        }

        return result;
    }

    // Mirrors AvailableSlot.NormalizeReservedForUserId — trim surrounding
    // whitespace, then treat empty/whitespace as "no reservation".
    private static string? NormaliseUserId(string? userId)
        => string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();

    // Dedup by user id (case-insensitive). Preserves the first occurrence so
    // a profile with richer traits (e.g. RequiresChargerForEveryRequest=true)
    // wins over a later duplicate with the trait off only when the caller
    // ordered them that way — duplicates are not expected in practice.
    private static List<CompanyCarCapacityUser> DistinctByUserId(
        IReadOnlyList<CompanyCarCapacityUser> users)
    {
        var seen = new HashSet<string>(UserIdComparer);
        var result = new List<CompanyCarCapacityUser>(users.Count);
        foreach (var user in users)
        {
            var normalised = NormaliseUserId(user.UserId);
            if (string.IsNullOrEmpty(normalised)) continue;
            if (seen.Add(normalised))
                result.Add(user with { UserId = normalised });
        }
        return result;
    }
}
