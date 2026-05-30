using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Tests.Startup;

public sealed class DataHubDbContextTests
{
    private static DataHubDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DataHubDbContext(options);
    }

    [Fact]
    public void EventInbox_DbSet_IsAccessible()
    {
        using var ctx = CreateInMemory();
        Assert.NotNull(ctx.EventInbox);
    }

    [Fact]
    public void ProjectionCheckpoints_DbSet_IsAccessible()
    {
        using var ctx = CreateInMemory();
        Assert.NotNull(ctx.ProjectionCheckpoints);
    }

    [Fact]
    public async Task EventInboxRecord_CanBeInsertedAndQueried()
    {
        using var ctx = CreateInMemory();

        var record = new EventInboxRecord
        {
            SourceEventId = "evt-001",
            EventName = "booking.drawCompleted",
            TenantId = "tenant-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = "{}"
        };

        ctx.EventInbox.Add(record);
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.EventInbox.SingleAsync(e => e.SourceEventId == "evt-001");
        Assert.Equal("booking.drawCompleted", retrieved.EventName);
        Assert.Equal("tenant-1", retrieved.TenantId);
        Assert.Null(retrieved.ProcessedAt);
    }

    [Fact]
    public async Task ProjectionCheckpoint_CanBeInsertedAndQueried()
    {
        using var ctx = CreateInMemory();

        var checkpoint = new ProjectionCheckpoint
        {
            ProjectionName = "BookingOutcomesProjection",
            LastProcessedEventId = "evt-100",
            LastProcessedAt = DateTimeOffset.UtcNow,
            EventCount = 100
        };

        ctx.ProjectionCheckpoints.Add(checkpoint);
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.ProjectionCheckpoints.FindAsync("BookingOutcomesProjection");
        Assert.NotNull(retrieved);
        Assert.Equal("evt-100", retrieved.LastProcessedEventId);
        Assert.Equal(100L, retrieved.EventCount);
    }

    [Fact]
    public async Task EventInboxRecord_ProcessedAt_DefaultsToNull()
    {
        using var ctx = CreateInMemory();

        ctx.EventInbox.Add(new EventInboxRecord
        {
            SourceEventId = "evt-002",
            EventName = "booking.requestSubmitted",
            TenantId = "tenant-2",
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = "{}"
        });
        await ctx.SaveChangesAsync();

        var record = await ctx.EventInbox.SingleAsync(e => e.SourceEventId == "evt-002");
        Assert.Null(record.ProcessedAt);
        Assert.Null(record.ProcessingError);
    }

    [Fact]
    public async Task EventInboxRecord_CanMarkProcessed()
    {
        using var ctx = CreateInMemory();

        var record = new EventInboxRecord
        {
            SourceEventId = "evt-003",
            EventName = "booking.drawStarted",
            TenantId = "tenant-1",
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = "{}"
        };
        ctx.EventInbox.Add(record);
        await ctx.SaveChangesAsync();

        record.ProcessedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.EventInbox.SingleAsync(e => e.SourceEventId == "evt-003");
        Assert.NotNull(retrieved.ProcessedAt);
    }
}
