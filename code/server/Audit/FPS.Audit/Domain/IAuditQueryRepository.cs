namespace FPS.Audit.Domain;

public sealed record AuditQueryRequest
{
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? EventType { get; init; }
    public string? ActorHash { get; init; }
    public DateTime? OccurredAfter { get; init; }
    public DateTime? OccurredBefore { get; init; }
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
