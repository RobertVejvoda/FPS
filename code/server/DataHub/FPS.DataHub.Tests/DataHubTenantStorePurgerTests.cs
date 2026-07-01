using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Tests;

// PLAT003C — destructive single-tenant purge of DataHub read models. Seeds all four tenant-scoped
// tables for two tenants (plus the global projection checkpoint), purges tenant A, and asserts that
// A's rows are gone, B's rows survive, the global checkpoint is untouched, and a second purge is a
// no-op. Uses RemoveRange + SaveChanges (not ExecuteDeleteAsync) so it runs under the EF InMemory
// provider these tests use, while staying correct in production.
public sealed class DataHubTenantStorePurgerTests
{
    private static DataHubDbContext NewDb() =>
        new(new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase($"PurgerTest_{Guid.NewGuid()}").Options);

    private static EventInboxRecord Inbox(string tenant, string sourceEventId) =>
        new()
        {
            SourceEventId = sourceEventId,
            EventName = "booking.requestSubmitted",
            TenantId = tenant,
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = "{}"
        };

    private static DrawHistoryProjection Draw(string tenant, string drawId) =>
        new() { DrawAttemptId = drawId, TenantId = tenant, LocationId = "loc", TimeSlot = "08:00-17:00", Date = new DateOnly(2026, 6, 3), Status = "Completed" };

    private static BookingOutcomeProjection Outcome(string tenant, string reqId, string requestor) =>
        new() { BookingRequestId = reqId, TenantId = tenant, RequestorId = requestor, LocationId = "loc", TimeSlot = "08:00-17:00", Date = new DateOnly(2026, 6, 3), FinalStatus = "Allocated" };

    private static TenantUsageStatsProjection Usage(string tenant) =>
        new() { TenantId = tenant, PeriodMonth = new DateOnly(2026, 6, 1), BookingRequestCount = 1 };

    private static async Task SeedTwoTenantsAsync(DataHubDbContext db)
    {
        db.EventInbox.AddRange(Inbox("tenant-a", "a-evt-1"), Inbox("tenant-a", "a-evt-2"), Inbox("tenant-b", "b-evt-1"));
        db.DrawHistory.AddRange(Draw("tenant-a", "a-draw-1"), Draw("tenant-b", "b-draw-1"));
        db.BookingOutcomes.AddRange(Outcome("tenant-a", "a-req-1", "u1"), Outcome("tenant-b", "b-req-1", "v1"));
        db.TenantUsageStats.AddRange(Usage("tenant-a"), Usage("tenant-b"));
        db.ProjectionCheckpoints.Add(new ProjectionCheckpoint
        {
            ProjectionName = "BookingOutcomesProjection",
            LastProcessedEventId = "evt-999",
            LastProcessedAt = DateTimeOffset.UtcNow,
            EventCount = 42
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PurgeAsync_RemovesAllRowsForTargetTenant_AcrossAllFourTables()
    {
        using var db = NewDb();
        await SeedTwoTenantsAsync(db);

        var removed = await new DataHubTenantStorePurger(db)
            .PurgeAsync(TenantPurgeScope.For("tenant-a"), sandboxReset: false, default);

        Assert.Equal(5, removed); // 2 inbox + 1 draw + 1 outcome + 1 usage
        Assert.Empty(await db.EventInbox.Where(x => x.TenantId == "tenant-a").ToListAsync());
        Assert.Empty(await db.DrawHistory.Where(x => x.TenantId == "tenant-a").ToListAsync());
        Assert.Empty(await db.BookingOutcomes.Where(x => x.TenantId == "tenant-a").ToListAsync());
        Assert.Empty(await db.TenantUsageStats.Where(x => x.TenantId == "tenant-a").ToListAsync());
    }

    [Fact]
    public async Task PurgeAsync_LeavesOtherTenantsAndGlobalCheckpointIntact()
    {
        using var db = NewDb();
        await SeedTwoTenantsAsync(db);

        await new DataHubTenantStorePurger(db)
            .PurgeAsync(TenantPurgeScope.For("tenant-a"), sandboxReset: false, default);

        Assert.Single(await db.EventInbox.Where(x => x.TenantId == "tenant-b").ToListAsync());
        Assert.Single(await db.DrawHistory.Where(x => x.TenantId == "tenant-b").ToListAsync());
        Assert.Single(await db.BookingOutcomes.Where(x => x.TenantId == "tenant-b").ToListAsync());
        Assert.Single(await db.TenantUsageStats.Where(x => x.TenantId == "tenant-b").ToListAsync());

        // Global projection checkpoint has no TenantId — it must never be touched by a tenant purge.
        var checkpoint = await db.ProjectionCheckpoints.FindAsync("BookingOutcomesProjection");
        Assert.NotNull(checkpoint);
        Assert.Equal(42L, checkpoint!.EventCount);
    }

    [Fact]
    public async Task PurgeAsync_IsIdempotent_SecondRunRemovesZero()
    {
        using var db = NewDb();
        await SeedTwoTenantsAsync(db);
        var purger = new DataHubTenantStorePurger(db);
        var scope = TenantPurgeScope.For("tenant-a");

        var first = await purger.PurgeAsync(scope, sandboxReset: false, default);
        var second = await purger.PurgeAsync(scope, sandboxReset: false, default);

        Assert.True(first > 0);
        Assert.Equal(0, second);
    }
}
