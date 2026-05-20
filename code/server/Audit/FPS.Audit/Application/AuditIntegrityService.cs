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

    // Deterministic hash of all audit evidence fields for each record in the range.
    // Records are sorted by (OccurredAt, AuditRecordId) for stability.
    // Payload is serialised and included in the hash so that content drift is detected.
    // Hashing Confidential data is safe because it is never returned in hash output or export.
    private static string ComputeHash(IReadOnlyList<AuditRecord> records)
    {
        using var sha = SHA256.Create();
        using var ms = new System.IO.MemoryStream();

        foreach (var r in records.OrderBy(r => r.OccurredAt).ThenBy(r => r.AuditRecordId))
        {
            // Each field that is part of the protected audit evidence is included.
            // Null values are represented as the literal "<null>" to distinguish from empty.
            var line = string.Join("|",
                r.SourceEventId,
                r.TenantId,
                r.EventType,
                r.EventVersion.ToString(),
                r.OccurredAt.ToString("O"),
                r.RecordedAt.ToString("O"),
                r.CorrelationId,
                r.CausationId ?? "<null>",
                r.ActorType,
                r.ActorHash ?? "<null>",
                r.Source,
                r.EntityType,
                r.EntityId ?? "<null>",
                r.Payload.ToJsonString());

            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            ms.Write(bytes, 0, bytes.Length);
        }

        ms.Position = 0;
        var hash = sha.ComputeHash(ms);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
