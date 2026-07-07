using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FPS.DataHub.Tests;

// #772 — user-level GDPR erasure of a single subject's report contribution in DataHub's durable
// projections. Only BookingOutcomeProjection carries a RequestorId, so these tests seed outcomes for
// two subjects across two tenants and assert: the target subject's rows are re-pointed to an
// irreversible pseudonym, other subjects/tenants are untouched, the operation is idempotent, and the
// anonymised state survives a fresh DbContext (i.e. it lives in the store, not process memory).
public sealed class DataHubSubjectEraserTests
{
    private static DbContextOptions<DataHubDbContext> Options(string dbName) =>
        new DbContextOptionsBuilder<DataHubDbContext>().UseInMemoryDatabase(dbName).Options;

    private static BookingOutcomeProjection Outcome(string tenant, string reqId, string requestor) =>
        new()
        {
            BookingRequestId = reqId, TenantId = tenant, RequestorId = requestor,
            LocationId = "loc", TimeSlot = "08:00-17:00", Date = new DateOnly(2026, 6, 3), FinalStatus = "Allocated"
        };

    private static async Task SeedAsync(DataHubDbContext db)
    {
        db.BookingOutcomes.AddRange(
            Outcome("tenant-a", "a-req-1", "u1"),   // target subject, row 1
            Outcome("tenant-a", "a-req-2", "u1"),   // target subject, row 2
            Outcome("tenant-a", "a-req-3", "u2"),   // other subject, same tenant
            Outcome("tenant-b", "b-req-1", "u1"));  // same id, other tenant
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AnonymiseSubject_RepointsTargetRows_ScopedByTenantAndSubject()
    {
        using var db = new DataHubDbContext(Options($"Eraser_{Guid.NewGuid()}"));
        await SeedAsync(db);

        var count = await new DataHubSubjectEraser(db).AnonymiseSubjectAsync("tenant-a", "u1");

        Assert.Equal(2, count);
        // Target subject's original id no longer appears in tenant-a.
        Assert.Empty(await db.BookingOutcomes.Where(b => b.TenantId == "tenant-a" && b.RequestorId == "u1").ToListAsync());
        // Both target rows carry the SAME fresh, irreversible pseudonym.
        var erased = await db.BookingOutcomes
            .Where(b => b.TenantId == "tenant-a" && (b.BookingRequestId == "a-req-1" || b.BookingRequestId == "a-req-2"))
            .ToListAsync();
        Assert.All(erased, r => Assert.StartsWith("erased:", r.RequestorId));
        Assert.Single(erased.Select(r => r.RequestorId).Distinct());
        Assert.DoesNotContain("u1", erased.Select(r => r.RequestorId));
        // Other subject in the same tenant is untouched.
        Assert.Equal("u2", (await db.BookingOutcomes.SingleAsync(b => b.BookingRequestId == "a-req-3")).RequestorId);
        // Same id in another tenant is untouched.
        Assert.Equal("u1", (await db.BookingOutcomes.SingleAsync(b => b.BookingRequestId == "b-req-1")).RequestorId);
    }

    [Fact]
    public async Task AnonymiseSubject_IsIdempotent_SecondCallAffectsNothing()
    {
        using var db = new DataHubDbContext(Options($"Eraser_{Guid.NewGuid()}"));
        await SeedAsync(db);
        var eraser = new DataHubSubjectEraser(db);

        Assert.Equal(2, await eraser.AnonymiseSubjectAsync("tenant-a", "u1"));
        Assert.Equal(0, await eraser.AnonymiseSubjectAsync("tenant-a", "u1"));
    }

    [Fact]
    public async Task AnonymiseSubject_SurvivesAcrossContexts_BecauseItLivesInTheStore()
    {
        // Shared in-memory database name = the same durable store across DbContext lifetimes,
        // standing in for a service restart: the anonymisation must still be there afterwards.
        var name = $"Eraser_shared_{Guid.NewGuid()}";
        using (var writer = new DataHubDbContext(Options(name)))
            await SeedAsync(writer);

        using (var eraserDb = new DataHubDbContext(Options(name)))
            Assert.Equal(2, await new DataHubSubjectEraser(eraserDb).AnonymiseSubjectAsync("tenant-a", "u1"));

        using var reader = new DataHubDbContext(Options(name));
        Assert.Empty(await reader.BookingOutcomes.Where(b => b.TenantId == "tenant-a" && b.RequestorId == "u1").ToListAsync());
        Assert.Equal(2, await reader.BookingOutcomes.CountAsync(b => b.RequestorId.StartsWith("erased:")));
    }

    [Theory]
    [InlineData("", "u1")]
    [InlineData("tenant-a", "")]
    [InlineData("tenant-a", "nobody")]
    public async Task AnonymiseSubject_NoMatch_ReturnsZero(string tenant, string requestor)
    {
        using var db = new DataHubDbContext(Options($"Eraser_{Guid.NewGuid()}"));
        await SeedAsync(db);

        Assert.Equal(0, await new DataHubSubjectEraser(db).AnonymiseSubjectAsync(tenant, requestor));
    }
}
