namespace FPS.Configuration.Domain;

public interface ISeatMapRepository
{
    Task<SeatMap> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);
    Task ReplaceLocationSeatMapAsync(string tenantId, string locationId, SeatMap map, CancellationToken cancellationToken = default);
}

public interface ISeatBlockRepository
{
    Task<IReadOnlyList<SeatBlock>> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);
    Task AddAsync(SeatBlock block, CancellationToken cancellationToken = default);
    /// <summary>Removes the block; returns false when no block with that id exists.</summary>
    Task<bool> RemoveAsync(string tenantId, string locationId, string blockId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Audit evidence for seat-map and seat-block changes, mirroring <see cref="SlotChangeRecord"/>:
/// who changed what, when, and why. Blocking changes affect capacity/fairness, so block add and
/// remove are recorded individually with the seat and date range.
/// </summary>
public sealed record SeatMapChangeRecord
{
    public const string TypeMapReplaced = "MapReplaced";
    public const string TypeSeatBlocked = "SeatBlocked";
    public const string TypeSeatUnblocked = "SeatUnblocked";

    public string TenantId { get; init; } = string.Empty;
    public string LocationId { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public string ChangedByUserId { get; init; } = string.Empty;
    public DateTimeOffset ChangedAt { get; init; }
    public string? ChangeReason { get; init; }
    // MapReplaced evidence.
    public int AreaCount { get; init; }
    public int SeatCount { get; init; }
    // SeatBlocked / SeatUnblocked evidence.
    public string? SeatId { get; init; }
    public DateOnly? BlockedFrom { get; init; }
    public DateOnly? BlockedTo { get; init; }
    public SeatBlockReason? BlockReason { get; init; }
}

public interface ISeatMapChangeRepository
{
    Task RecordAsync(SeatMapChangeRecord change, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeatMapChangeRecord>> GetHistoryAsync(string tenantId, string locationId, int limit, CancellationToken cancellationToken = default);
}
