namespace FPS.Audit.Domain;

public sealed class AuditRecord
{
    public Guid AuditRecordId { get; init; }
    public string SourceEventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public int EventVersion { get; init; }
    public DateTime OccurredAt { get; init; }
    public DateTime RecordedAt { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string? CausationId { get; init; }
    public string ActorType { get; init; } = string.Empty;
    public string? ActorHash { get; init; }
    public string Source { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public object Payload { get; init; } = new();
}

public interface IAuditRepository
{
    Task<bool> ExistsAsync(string sourceEventId, CancellationToken cancellationToken = default);
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
