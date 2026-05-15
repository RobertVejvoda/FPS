namespace FPS.Configuration.Domain;

public sealed record ParkingSlot
{
    public string SlotId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool HasCharger { get; init; }
    public bool IsAccessible { get; init; }
    public bool IsCompanyCarOnly { get; init; }
    public bool IsMotorcycleCapacity { get; init; }
    public string? ReservedForUserId { get; init; }
}
