using FPS.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Infrastructure;

/// <summary>
/// Purges all DataHub-owned read-model data for a single tenant (PLAT003C). DataHub is a
/// PostgreSQL/EF Core read store, so this deletes the four tenant-scoped projection/inbox tables
/// by their <c>TenantId</c> column. The global <c>datahub_projection_checkpoint</c> has no
/// TenantId and is intentionally left untouched. Not immutable evidence, so it runs on a normal
/// tenant purge as well as a sandbox reset.
/// </summary>
public sealed class DataHubTenantStorePurger(DataHubDbContext db) : ITenantStorePurger
{
    public string Service => "datahub";

    public bool IsImmutableEvidence => false;

    public async Task<int> PurgeAsync(TenantPurgeScope scope, bool sandboxReset, CancellationToken ct)
    {
        var tenantId = scope.TenantId;

        var eventInbox = await db.EventInbox.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var drawHistory = await db.DrawHistory.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var bookingOutcomes = await db.BookingOutcomes.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var usageStats = await db.TenantUsageStats.Where(x => x.TenantId == tenantId).ToListAsync(ct);

        db.EventInbox.RemoveRange(eventInbox);
        db.DrawHistory.RemoveRange(drawHistory);
        db.BookingOutcomes.RemoveRange(bookingOutcomes);
        db.TenantUsageStats.RemoveRange(usageStats);

        // datahub_projection_checkpoint is global (no TenantId) — intentionally left untouched.
        await db.SaveChangesAsync(ct);

        return eventInbox.Count + drawHistory.Count + bookingOutcomes.Count + usageStats.Count;
    }
}
