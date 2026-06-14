namespace FPS.Configuration.Domain;

public sealed record ParkingSlot
{
    // Default capacity for a motorcycle-specific slot when no per-slot count is set.
    // Per the v1 product rule on #468, a motorcycle area holds up to 4 motorcycles.
    public const int DefaultMotorcycleCapacityUnits = 4;

    public string SlotId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool HasCharger { get; init; }
    public bool IsAccessible { get; init; }
    public bool IsCompanyCarOnly { get; init; }
    public bool IsMotorcycleCapacity { get; init; }
    // Configurable number of motorcycles that fit in this motorcycle-specific slot.
    // Null means "use the default" (DefaultMotorcycleCapacityUnits) when the slot is
    // marked IsMotorcycleCapacity. Ignored for non-motorcycle slots.
    public int? MotorcycleCapacityUnits { get; init; }
    public string? ReservedForUserId { get; init; }

    // Resolved units count: defaults to DefaultMotorcycleCapacityUnits when the slot
    // is motorcycle-specific and no per-slot value is set. Returns 1 for non-motorcycle
    // slots so callers can treat every slot as "N allocatable units" without branching.
    public int EffectiveMotorcycleCapacityUnits =>
        IsMotorcycleCapacity ? (MotorcycleCapacityUnits ?? DefaultMotorcycleCapacityUnits) : 1;
}
