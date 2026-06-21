namespace FPS.SharedKernel.Capacity;

// Issue #533: shared helper for the company-car fixed-slot capacity warning.
// The slot-side semantics intentionally mirror CompanyCarReservedSlotRules.Resolve
// in Booking — a slot only counts as "guaranteed capacity" when it is active,
// company-car-only, and reserved for a specific user. This is the same shape
// PR #529 uses for the immediate Tier 1 allocation, so the Configuration
// warning surfaces the exact set of guarantees the allocator will honor.
//
// The helper is intentionally cross-service-free: callers pass their own
// snapshot of slots (Configuration) and company-car users (Profile). That keeps
// the comparison deterministic and unit-testable without HTTP plumbing.

/// <summary>
/// A minimal projection of a parking slot needed to classify it as a
/// guaranteed company-car fixed slot. Mirrors the Configuration ParkingSlot
/// shape but stays free of any service-specific dependency.
/// </summary>
public readonly record struct CompanyCarCapacitySlot(
    string LocationId,
    bool IsActive,
    bool IsCompanyCarOnly,
    string? ReservedForUserId);

/// <summary>
/// Computed warning for a single location.
/// </summary>
/// <param name="LocationId">Configuration location id this row refers to.</param>
/// <param name="CompanyCarEmployeeCount">
/// Active company-car employees assigned to this location.
/// </param>
/// <param name="ActiveCompatibleFixedSlotCount">
/// Active, company-car-only slots reserved for a specific user at this location.
/// Mirrors the "active compatible fixed slot" rule used by the allocator.
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

        // Filter slots to the "active compatible fixed company-car slot"
        // definition. This is what PR #529 (CompanyCarReservedSlotRules.Resolve)
        // treats as a guarantee, so the warning will not over-promise.
        var guaranteedByLocation = slots
            .Where(s => s.IsActive
                        && s.IsCompanyCarOnly
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
