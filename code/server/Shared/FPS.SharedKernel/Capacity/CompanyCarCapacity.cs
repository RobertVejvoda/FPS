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
//   * HasCharger / IsAccessible are vehicle-trait gates (electric / accessibility).
//     The warning is vehicle-trait-agnostic; HR can inspect the slot row for
//     finer compatibility details if needed.
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
public readonly record struct CompanyCarCapacitySlot(
    string LocationId,
    bool IsActive,
    bool IsMotorcycleCapacity,
    string? ReservedForUserId);

/// <summary>
/// Computed warning for a single location.
/// </summary>
/// <param name="LocationId">Configuration location id this row refers to.</param>
/// <param name="CompanyCarEmployeeCount">
/// Active company-car employees assigned to this location.
/// </param>
/// <param name="ActiveCompatibleFixedSlotCount">
/// Distinct users covered by an active fixed slot reserved for them at this
/// location (excluding motorcycle-only bays). Mirrors what the allocator will
/// honor as an immediate Tier 1 allocation for a company-car request.
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
    /// <summary>
    /// Compute one warning row per location. A location appears in the result
    /// when it has at least one company-car employee assigned OR at least one
    /// active compatible fixed company-car slot. Locations with neither are
    /// omitted so the UI does not show noise for unrelated sites.
    /// </summary>
    /// <param name="companyCarUsersByLocation">
    /// For each location, the distinct user ids of active company-car employees
    /// assigned to that location (e.g. by HomeLocationId).
    /// </param>
    /// <param name="slots">All known slots across all locations for the tenant.</param>
    public static IReadOnlyList<CompanyCarCapacityWarning> Compute(
        IReadOnlyDictionary<string, IReadOnlyList<string>> companyCarUsersByLocation,
        IEnumerable<CompanyCarCapacitySlot> slots)
    {
        ArgumentNullException.ThrowIfNull(companyCarUsersByLocation);
        ArgumentNullException.ThrowIfNull(slots);

        // Filter slots to the "active compatible fixed slot for the requestor"
        // definition the allocator uses. Motorcycle-only bays are the one
        // compatibility gate that always rejects company-car vehicles, so they
        // cannot count as a guarantee.
        var guaranteedByLocation = slots
            .Where(s => s.IsActive
                        && !s.IsMotorcycleCapacity
                        && !string.IsNullOrEmpty(s.ReservedForUserId))
            .GroupBy(s => s.LocationId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<string>)g
                    .Select(s => s.ReservedForUserId!)
                    .Distinct(StringComparer.Ordinal)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        var locations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in companyCarUsersByLocation.Keys) locations.Add(k);
        foreach (var k in guaranteedByLocation.Keys) locations.Add(k);

        var result = new List<CompanyCarCapacityWarning>(locations.Count);
        foreach (var locationId in locations.OrderBy(l => l, StringComparer.Ordinal))
        {
            var users = companyCarUsersByLocation.TryGetValue(locationId, out var u)
                ? u.Distinct(StringComparer.Ordinal).ToList()
                : new List<string>();
            var guaranteed = guaranteedByLocation.TryGetValue(locationId, out var g)
                ? g
                : (IReadOnlyCollection<string>)Array.Empty<string>();

            // Distinct-count of unique reserved users. We dedup on the slot side
            // because a single user reserved to multiple slots still counts as
            // one guarantee for THAT user — the surplus does not cover anyone
            // else (slots are user-specific).
            var fixedSlotCount = guaranteed.Count;

            var withoutGuarantee = users
                .Count(uid => !guaranteed.Contains(uid));

            result.Add(new CompanyCarCapacityWarning(
                LocationId: locationId,
                CompanyCarEmployeeCount: users.Count,
                ActiveCompatibleFixedSlotCount: fixedSlotCount,
                EmployeesWithoutGuaranteedSlot: withoutGuarantee,
                IsCapacityExceeded: withoutGuarantee > 0));
        }

        return result;
    }
}
