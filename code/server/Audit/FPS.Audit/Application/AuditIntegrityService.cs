using FPS.Audit.Domain;
using System.Security.Cryptography;
using System.Text;

namespace FPS.Audit.Application;

public sealed record IntegrityVerificationResult(
    string TenantId,
    DateTime CheckedFrom,
    DateTime CheckedTo,
    int RecordCount,
    string IntegrityHash,
    bool HasMismatch,
    int MismatchCount,
    DateTime VerifiedAt,
    string Result);

// Safe export record: excludes raw Payload (may contain Confidential data).
// ActorHash is pseudonymised and safe for operator review.
public sealed record AuditExportRecord(
    Guid AuditRecordId,
    string EventType,
    int EventVersion,
    DateTime OccurredAt,
    string CorrelationId,
    string ActorType,
    string? ActorHash,
    string Source,
    string EntityType,
    string? EntityId);

public sealed class AuditIntegrityService(IAuditRetentionRepository repository)
{
    public async Task<IntegrityVerificationResult> VerifyAsync(
        string tenantId,
        DateTime from,
        DateTime to,
        string? expectedHash = null,
        CancellationToken cancellationToken = default)
    {
        var records = await repository.GetRangeAsync(tenantId, from, to, maxRecords: 10_000, cancellationToken);
        var hash = ComputeHash(records);
        var hasMismatch = expectedHash is not null && !string.Equals(hash, expectedHash, StringComparison.OrdinalIgnoreCase);

        return new IntegrityVerificationResult(
            TenantId: tenantId,
            CheckedFrom: from,
            CheckedTo: to,
            RecordCount: records.Count,
            IntegrityHash: hash,
            HasMismatch: hasMismatch,
            MismatchCount: hasMismatch ? 1 : 0,
            VerifiedAt: DateTime.UtcNow,
            Result: hasMismatch ? "mismatch" : "ok");
    }

    public async Task<IReadOnlyList<AuditExportRecord>> ExportAsync(
        string tenantId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var records = await repository.GetRangeAsync(tenantId, from, to, maxRecords: 10_000, cancellationToken);

        return records.Select(r => new AuditExportRecord(
            AuditRecordId: r.AuditRecordId,
            EventType: r.EventType,
            EventVersion: r.EventVersion,
            OccurredAt: r.OccurredAt,
            CorrelationId: r.CorrelationId,
            ActorType: r.ActorType,
            ActorHash: r.ActorHash,
            Source: r.Source,
            EntityType: r.EntityType,
            EntityId: r.EntityId)).ToList();
    }

    // Deterministic hash of (sorted by OccurredAt + AuditRecordId) record identifiers.
    // Detects insertion or deletion of records; payload content is not hashed to avoid
    // including Confidential data in the hash input.
    private static string ComputeHash(IReadOnlyList<AuditRecord> records)
    {
        var sb = new StringBuilder();
        foreach (var r in records.OrderBy(r => r.OccurredAt).ThenBy(r => r.AuditRecordId))
            sb.Append($"{r.SourceEventId}|{r.EventType}|{r.TenantId}|");

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
