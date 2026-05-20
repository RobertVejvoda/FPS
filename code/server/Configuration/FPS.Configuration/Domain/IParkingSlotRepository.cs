namespace FPS.Configuration.Domain;

public interface IParkingSlotRepository
{
    Task<IReadOnlyList<ParkingSlot>> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);
    Task ReplaceLocationSlotsAsync(string tenantId, string locationId, IReadOnlyList<ParkingSlot> slots, CancellationToken cancellationToken = default);
}

public sealed record SlotChangeRecord
{
    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string ChangedByUserId { get; init; } = string.Empty;
    public DateTimeOffset ChangedAt { get; init; }
    public string? ChangeReason { get; init; }
    public int SlotCount { get; init; }
}

public interface ISlotChangeRepository
{
    Task RecordAsync(SlotChangeRecord change, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SlotChangeRecord>> GetHistoryAsync(string tenantId, string locationId, int limit = 20, CancellationToken cancellationToken = default);
}
