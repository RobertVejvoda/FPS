namespace FPS.Audit.Domain;

/// <summary>
/// Activity category groupings for auditor workspace filtering (AUDIT003).
/// </summary>
public enum ActivityCategory
{
    All,
    BookingLifecycle,
    DrawEvents,
    PolicyChanges,
    Notifications,
    PrivacyErasure,
    ManualCorrections
}

public sealed record AuditQueryRequest
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? EventType { get; init; }
    public string? ActorHash { get; init; }
    // Short stable reference prefix — matches the first N chars of ActorHash (case-insensitive).
    // Auditors search using the displayed short ref (e.g. "A3F1B2") rather than the full hash.
    public string? ActorRef { get; init; }
    public DateTime? OccurredAfter { get; init; }
    public DateTime? OccurredBefore { get; init; }
    // Business activity filters (AUD006)
    public string? Action { get; init; }
    public string? Result { get; init; }
    public string? ReasonCode { get; init; }
    public string? TraceId { get; init; }
    // Activity category grouping (AUDIT003)
    public ActivityCategory? Category { get; init; }
    public int PageSize { get; init; } = 50;
    public int Page { get; init; } = 1;

    public int SafePageSize => Math.Clamp(PageSize, 1, 100);
    public int SafePage => Math.Max(1, Page);
}

public interface IAuditQueryRepository
{
    Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> QueryAsync(
        AuditQueryRequest query, string tenantId, CancellationToken cancellationToken = default);
}
