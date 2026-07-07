using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Infrastructure;

/// <summary>
/// User-level (single-subject) GDPR erasure for DataHub's durable report projections (#772).
/// Since #763 Reporting reads its report data from here, this is where a subject's contribution
/// to the durable reports must be anonymised. Only <see cref="Domain.BookingOutcomeProjection"/>
/// carries a subject reference (<c>RequestorId</c>); the other projections are tenant/aggregate
/// only. The subject's rows are re-pointed to a fresh irreversible pseudonym (not deleted), so
/// aggregate metrics stay intact while the personal link is removed. Anonymisation is naturally
/// idempotent: once a subject's rows carry the pseudonym, the original id matches nothing.
/// </summary>
public sealed class DataHubSubjectEraser(DataHubDbContext db)
{
    public async Task<int> AnonymiseSubjectAsync(string tenantId, string requestorRef, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(requestorRef))
            return 0;

        var rows = await db.BookingOutcomes
            .Where(b => b.TenantId == tenantId && b.RequestorId == requestorRef)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return 0;

        // One irreversible pseudonym for this subject's whole history (keeps their rows grouped
        // as a single anonymous actor without any link back to the real id).
        var pseudonym = "erased:" + Guid.NewGuid().ToString("N");
        foreach (var row in rows)
            row.RequestorId = pseudonym;

        await db.SaveChangesAsync(ct);
        return rows.Count;
    }
}
