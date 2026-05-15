using System.Text.Json.Nodes;

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
    public JsonObject Payload { get; init; } = new();
}

public interface IAuditRepository
{
    Task<bool> ExistsAsync(string sourceEventId, CancellationToken cancellationToken = default);
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);
}

// Shape per audit.md pseudonymisation rules; A002 adds persistence and erasure.
public sealed class PiiMapping
{
    public string TenantId { get; init; } = string.Empty;
    public string ActorHash { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Email { get; init; }
}

public interface IPiiMappingRepository
{
    Task SaveAsync(PiiMapping mapping, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(string userId, string tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string userId, string tenantId, CancellationToken cancellationToken = default);
}
