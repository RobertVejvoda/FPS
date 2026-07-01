using FPS.DataHub.Application;
using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Tests;

// PLAT005A — the monthly usage ledger is recomputed from the (idempotent) booking-outcome and
// draw-history projections, so these tests seed those projections directly and assert the rollup,
// idempotency under duplicate delivery, and cross-tenant separation.
public sealed class UsageStatsProjectionHandlerTests
{
    private static DataHubDbContext NewDb() =>
        new(new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase($"UsageStatsTest_{Guid.NewGuid()}").Options);

    private static BookingOutcomeProjection Outcome(string tenant, string reqId, string requestor, DateOnly date, string status) =>
        new() { BookingRequestId = reqId, TenantId = tenant, RequestorId = requestor, LocationId = "loc", TimeSlot = "08:00-17:00", Date = date, FinalStatus = status };

    private static DrawHistoryProjection Draw(string tenant, string drawId, DateOnly date) =>
        new() { DrawAttemptId = drawId, TenantId = tenant, LocationId = "loc", TimeSlot = "08:00-17:00", Date = date, Status = "Completed" };

    // Trigger event whose payload date selects the month to recompute.
    private static BookingEventEnvelope Trigger(string tenant, DateOnly inMonth) =>
        new(
            EventId: $"evt-{Guid.NewGuid()}",
            EventType: "booking.requestSubmitted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: tenant,
            CorrelationId: "corr",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null, RequestorId: null, LocationId: null,
                Date: inMonth.ToString("yyyy-MM-dd"), TimeSlot: null,
                PreviousStatus: null, NewStatus: null, ReasonCode: null, ReasonText: null,
                AffectedRecipientIds: null));

    [Fact]
    public async Task Recompute_AggregatesOutcomesAndDrawsForTheMonth()
    {
        using var db = NewDb();
        var june = new DateOnly(2026, 6, 1);
        db.BookingOutcomes.AddRange(
            Outcome("tenant-a", "a1", "u1", new(2026, 6, 3), "Allocated"),
            Outcome("tenant-a", "a2", "u2", new(2026, 6, 10), "Rejected"),
            Outcome("tenant-a", "a3", "u1", new(2026, 6, 20), "Used"),       // u1 again → 2 distinct requestors
            Outcome("tenant-a", "a4", "u3", new(2026, 7, 2), "Allocated"));  // July → excluded
        db.DrawHistory.AddRange(
            Draw("tenant-a", "d1", new(2026, 6, 3)),
            Draw("tenant-a", "d2", new(2026, 6, 10)),
            Draw("tenant-a", "d3", new(2026, 7, 1)));                        // July → excluded
        await db.SaveChangesAsync();

        await new UsageStatsProjectionHandler(db).HandleAsync(Trigger("tenant-a", new(2026, 6, 15)), default);

        var row = await db.TenantUsageStats.SingleAsync(u => u.TenantId == "tenant-a" && u.PeriodMonth == june);
        Assert.Equal(3, row.BookingRequestCount);
        Assert.Equal(2, row.ActiveRequestorCount);
        Assert.Equal(1, row.AllocatedCount);
        Assert.Equal(1, row.RejectedCount);
        Assert.Equal(1, row.UsedCount);
        Assert.Equal(0, row.CancelledCount);
        Assert.Equal(2, row.DrawRunCount);
    }

    [Fact]
    public async Task DuplicateDelivery_DoesNotDoubleCount()
    {
        using var db = NewDb();
        db.BookingOutcomes.AddRange(
            Outcome("tenant-a", "a1", "u1", new(2026, 6, 3), "Allocated"),
            Outcome("tenant-a", "a2", "u2", new(2026, 6, 4), "Rejected"));
        await db.SaveChangesAsync();
        var handler = new UsageStatsProjectionHandler(db);

        await handler.HandleAsync(Trigger("tenant-a", new(2026, 6, 15)), default);
        await handler.HandleAsync(Trigger("tenant-a", new(2026, 6, 15)), default); // duplicate delivery
        await handler.HandleAsync(Trigger("tenant-a", new(2026, 6, 15)), default); // and again

        var rows = await db.TenantUsageStats.Where(u => u.TenantId == "tenant-a").ToListAsync();
        Assert.Single(rows);
        Assert.Equal(2, rows[0].BookingRequestCount);
        Assert.Equal(1, rows[0].AllocatedCount);
        Assert.Equal(1, rows[0].RejectedCount);
    }

    [Fact]
    public async Task Recompute_KeepsTenantsSeparate()
    {
        using var db = NewDb();
        var june = new DateOnly(2026, 6, 1);
        db.BookingOutcomes.AddRange(
            Outcome("tenant-a", "a1", "u1", new(2026, 6, 3), "Allocated"),
            Outcome("tenant-a", "a2", "u2", new(2026, 6, 4), "Allocated"),
            Outcome("tenant-b", "b1", "v1", new(2026, 6, 5), "Rejected"));
        await db.SaveChangesAsync();
        var handler = new UsageStatsProjectionHandler(db);

        await handler.HandleAsync(Trigger("tenant-a", new(2026, 6, 15)), default);
        await handler.HandleAsync(Trigger("tenant-b", new(2026, 6, 15)), default);

        var a = await db.TenantUsageStats.SingleAsync(u => u.TenantId == "tenant-a" && u.PeriodMonth == june);
        var b = await db.TenantUsageStats.SingleAsync(u => u.TenantId == "tenant-b" && u.PeriodMonth == june);
        Assert.Equal(2, a.BookingRequestCount);
        Assert.Equal(2, a.AllocatedCount);
        Assert.Equal(1, b.BookingRequestCount);
        Assert.Equal(1, b.RejectedCount);
        Assert.Equal(0, b.AllocatedCount);
    }
}
