using System.Text.Json;
using System.Text.Json.Nodes;
using FPS.Audit.Domain;

namespace FPS.Audit.Application;

public sealed record AuditRecordResponse(
    Guid AuditRecordId,
    string SourceEventId,
    string EventType,
    int EventVersion,
    DateTime OccurredAt,
    DateTime RecordedAt,
    string CorrelationId,
    string? CausationId,
    string ActorType,
    string? ActorHash,
    string Source,
    string EntityType,
    string? EntityId,
    JsonElement Payload)
{
    public static AuditRecordResponse From(AuditRecord record) => new(
        AuditRecordId: record.AuditRecordId,
        SourceEventId: record.SourceEventId,
        EventType: record.EventType,
        EventVersion: record.EventVersion,
        OccurredAt: record.OccurredAt,
        RecordedAt: record.RecordedAt,
        CorrelationId: record.CorrelationId,
        CausationId: record.CausationId,
        ActorType: record.ActorType,
        ActorHash: record.ActorHash,
        Source: record.Source,
        EntityType: record.EntityType,
        EntityId: record.EntityId,
        Payload: JsonSerializer.SerializeToElement(record.Payload));
}

public sealed record PagedAuditResponse(
    IReadOnlyList<AuditRecordResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed class AuditQueryService(IAuditQueryRepository repository)
{
    public async Task<PagedAuditResponse> QueryAsync(
        AuditQueryRequest query, string tenantId, CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await repository.QueryAsync(query, tenantId, cancellationToken);
        return new PagedAuditResponse(
            Items: items.Select(AuditRecordResponse.From).ToList(),
            TotalCount: totalCount,
            Page: query.SafePage,
            PageSize: query.SafePageSize);
    }
}
