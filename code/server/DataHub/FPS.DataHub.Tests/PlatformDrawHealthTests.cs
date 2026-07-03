using FPS.DataHub.Controllers;
using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Tests;

// PLAT008E — draw-health freshness/stale/stuck logic. Tests the controller directly against a fresh
// in-memory context per test (no shared WebApplicationFactory db), so count-sensitive cases stay
// isolated. Pins the "never a false green" contract Codex asked for: missing evidence, stale
// activity, and an old Running draw outside the window must NOT read as healthy.
public sealed class PlatformDrawHealthTests
{
    private static DataHubDbContext NewDb() =>
        new(new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase($"DrawHealth_{Guid.NewGuid()}").Options);

    private static async Task<DrawHealthDto> GetAsync(DataHubDbContext db, int windowDays = 7)
    {
        var result = await new PlatformDrawHealthController(db).Get(windowDays, CancellationToken.None);
        return Assert.IsType<DrawHealthDto>(Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task NoDrawEvidence_IsNotHealthy()
    {
        await using var db = NewDb();

        var dto = await GetAsync(db);

        // No rows at all → the strip can't prove health, so it must not be green.
        Assert.False(dto.HasEvidence);
        Assert.False(dto.Stale);
        Assert.Equal(0, dto.CompletedCount);
        Assert.Equal(0, dto.FailedCount);
        Assert.Equal(0, dto.StuckCount);
        Assert.Null(dto.LastActivityAt);
    }

    [Fact]
    public async Task StaleLastActivity_IsFlaggedStale()
    {
        await using var db = NewDb();
        var old = DateTime.UtcNow.AddDays(-20);
        db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = "d-old", TenantId = "t", LocationId = "L", Status = "Completed",
            StartedAt = old, CompletedAt = old.AddMinutes(1), LastUpdatedAt = old,
        });
        await db.SaveChangesAsync();

        var dto = await GetAsync(db, windowDays: 7);

        Assert.True(dto.HasEvidence);
        Assert.True(dto.Stale);            // evidence exists but nothing updated within the window
        Assert.Equal(0, dto.CompletedCount); // the completed draw is outside the recent window
    }

    [Fact]
    public async Task OldRunningDraw_OutsideWindow_IsStillCountedStuck()
    {
        await using var db = NewDb();
        var old = DateTime.UtcNow.AddDays(-40);
        db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = "d-run", TenantId = "t", LocationId = "L", Status = "Running",
            StartedAt = old, CompletedAt = null, LastUpdatedAt = old,
        });
        await db.SaveChangesAsync();

        var dto = await GetAsync(db, windowDays: 7);

        // A draw that started but never completed is a red flag regardless of age — not filtered out.
        Assert.Equal(1, dto.StuckCount);
        Assert.Equal(1, dto.RunningCount);
        Assert.True(dto.Stale);
    }

    [Fact]
    public async Task FreshCleanDraws_AreOk()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = "d-ok", TenantId = "t", LocationId = "L", Status = "Completed",
            StartedAt = now.AddMinutes(-5), CompletedAt = now.AddMinutes(-4), LastUpdatedAt = now.AddMinutes(-4),
        });
        await db.SaveChangesAsync();

        var dto = await GetAsync(db);

        Assert.True(dto.HasEvidence);
        Assert.False(dto.Stale);
        Assert.Equal(1, dto.CompletedCount);
        Assert.Equal(0, dto.FailedCount);
        Assert.Equal(0, dto.StuckCount);
    }

    [Fact]
    public async Task RecentFailure_IsCounted()
    {
        await using var db = NewDb();
        var now = DateTime.UtcNow;
        db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = "d-fail", TenantId = "t", LocationId = "L", Status = "Failed",
            StartedAt = now.AddMinutes(-10), CompletedAt = now.AddMinutes(-9),
            SafeFailureReason = "reason", LastUpdatedAt = now.AddMinutes(-9),
        });
        await db.SaveChangesAsync();

        var dto = await GetAsync(db);

        Assert.Equal(1, dto.FailedCount);
        Assert.NotNull(dto.LastFailureAt);
    }
}
