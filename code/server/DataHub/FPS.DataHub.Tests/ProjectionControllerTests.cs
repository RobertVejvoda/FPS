using FPS.DataHub.Application;
using FPS.DataHub.Controllers;
using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.DataHub.Tests;

// Minimal stub — controllers only need TenantId and UserId for query filtering
file sealed class FakeCurrentUser(string tenantId, string userId) : ICurrentUser
{
    public string TenantId { get; } = tenantId;
    public string UserId { get; } = userId;
    public IReadOnlyList<string> Roles => [];
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => false;
}

public sealed class ProjectionControllerTests : IDisposable
{
    private readonly DataHubDbContext _db;

    public ProjectionControllerTests()
    {
        var options = new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase($"ControllerTest_{Guid.NewGuid()}")
            .Options;
        _db = new DataHubDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── Draw identity linkage (Finding 1 regression) ──────────────────────────

    [Fact]
    public async Task HandleDrawStarted_PrefersPayloadDrawAttemptId_OverEventId()
    {
        var handler = new BookingProjectionHandler(_db, NullLogger<BookingProjectionHandler>.Instance);

        var envelope = MakeDrawStarted(
            eventId: "evt-999",
            drawAttemptId: "draw:t1:loc1:2026-06-10:0800-1700");

        await handler.HandleAsync(envelope, CancellationToken.None);

        var row = await _db.DrawHistory.SingleAsync();
        Assert.Equal("draw:t1:loc1:2026-06-10:0800-1700", row.DrawAttemptId);
    }

    [Fact]
    public async Task HandleDrawCompleted_LinksToStarted_ViaPayloadDrawAttemptId()
    {
        var handler = new BookingProjectionHandler(_db, NullLogger<BookingProjectionHandler>.Instance);
        var stableId = "draw:t1:loc1:2026-06-11:0800-1700";

        // started event arrives first with stable drawAttemptId in payload
        await handler.HandleAsync(
            MakeDrawStarted("evt-start", stableId),
            CancellationToken.None);

        // completed event carries the same drawAttemptId in payload (no causation link)
        await handler.HandleAsync(
            MakeDrawCompleted("evt-complete", stableId, causationId: null),
            CancellationToken.None);

        var row = await _db.DrawHistory.SingleAsync();
        Assert.Equal("Completed", row.Status);
        Assert.Equal(3, row.AllocatedCount);
        Assert.Equal(1, row.RejectedCount);
    }

    [Fact]
    public async Task HandleDrawCompleted_WithoutStarted_UsesPayloadDrawAttemptIdForUpsert()
    {
        var handler = new BookingProjectionHandler(_db, NullLogger<BookingProjectionHandler>.Instance);

        await handler.HandleAsync(
            MakeDrawCompleted("evt-complete-only", "draw:t1:loc1:2026-06-12:0800-1700", causationId: null),
            CancellationToken.None);

        var row = await _db.DrawHistory.SingleAsync();
        Assert.Equal("draw:t1:loc1:2026-06-12:0800-1700", row.DrawAttemptId);
        Assert.Equal("Completed", row.Status);
    }

    // ── Employee privacy — /datahub/my-outcomes ───────────────────────────────

    [Fact]
    public async Task GetMyOutcomes_ReturnsOnlyCallerOutcomes_NotOtherEmployees()
    {
        await SeedOutcome("req-alice-1", "tenant-a", "alice");
        await SeedOutcome("req-bob-1", "tenant-a", "bob");

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "alice"));
        var result = await ctrl.GetMyOutcomes(ct: default) as OkObjectResult;

        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("req-alice-1", json);
        Assert.DoesNotContain("req-bob-1", json);
    }

    [Fact]
    public async Task GetMyOutcomes_CrossTenantIsolation_ReturnsEmpty()
    {
        await SeedOutcome("req-t2-1", "tenant-b", "alice");

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "alice"));
        var result = await ctrl.GetMyOutcomes(ct: default) as OkObjectResult;

        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.DoesNotContain("req-t2-1", json);
    }

    // ── HR Draw history tenant isolation ─────────────────────────────────────

    [Fact]
    public async Task GetDrawHistory_ReturnsOnlyCallerTenantDraws()
    {
        await SeedDraw("draw-a1", "tenant-a");
        await SeedDraw("draw-b1", "tenant-b");

        var ctrl = new DrawHistoryController(_db, new FakeCurrentUser("tenant-a", "hr-user"));
        var result = await ctrl.GetDrawHistory(ct: default) as OkObjectResult;

        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("draw-a1", json);
        Assert.DoesNotContain("draw-b1", json);
    }

    [Fact]
    public async Task GetDrawOutcomes_ReturnsTenantDrawWithOutcomes()
    {
        await SeedDraw("draw-a2", "tenant-a");
        await SeedOutcomeForDraw("req-x1", "tenant-a", "alice", "draw-a2");
        await SeedOutcomeForDraw("req-x2", "tenant-a", "bob", "draw-a2");

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "hr-user"));
        var result = await ctrl.GetDrawOutcomes("draw-a2", ct: default) as OkObjectResult;

        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("req-x1", json);
        Assert.Contains("req-x2", json);
    }

    [Fact]
    public async Task GetDrawOutcomes_IncludesLegacyMatchingOutcomesWithoutDrawAttemptId()
    {
        await SeedDraw("draw-a-legacy", "tenant-a");
        _db.BookingOutcomes.Add(new BookingOutcomeProjection
        {
            BookingRequestId = "req-legacy",
            TenantId = "tenant-a",
            RequestorId = "alice",
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 10),
            TimeSlot = "08:00-17:00",
            FinalStatus = "Submitted",
            DrawAttemptId = null
        });
        await _db.SaveChangesAsync();

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "hr-user"));
        var result = await ctrl.GetDrawOutcomes("draw-a-legacy", ct: default) as OkObjectResult;

        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("req-legacy", json);
        Assert.Contains("Waitlisted", json);
    }

    [Fact]
    public async Task GetDrawOutcomes_CrossTenant_ReturnsNotFound()
    {
        await SeedDraw("draw-b2", "tenant-b");

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "hr-user"));
        var result = await ctrl.GetDrawOutcomes("draw-b2", ct: default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SeedOutcome(string requestId, string tenantId, string requestorId)
    {
        _db.BookingOutcomes.Add(new BookingOutcomeProjection
        {
            BookingRequestId = requestId,
            TenantId = tenantId,
            RequestorId = requestorId,
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 10),
            TimeSlot = "08:00-17:00",
            FinalStatus = "Allocated"
        });
        await _db.SaveChangesAsync();
    }

    private async Task SeedOutcomeForDraw(string requestId, string tenantId, string requestorId, string drawAttemptId)
    {
        _db.BookingOutcomes.Add(new BookingOutcomeProjection
        {
            BookingRequestId = requestId,
            TenantId = tenantId,
            RequestorId = requestorId,
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 10),
            TimeSlot = "08:00-17:00",
            FinalStatus = "Allocated",
            DrawAttemptId = drawAttemptId
        });
        await _db.SaveChangesAsync();
    }

    private async Task SeedDraw(string drawAttemptId, string tenantId)
    {
        _db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = drawAttemptId,
            TenantId = tenantId,
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 10),
            TimeSlot = "08:00-17:00",
            Status = "Completed",
            AllocatedCount = 2,
            RejectedCount = 1
        });
        await _db.SaveChangesAsync();
    }

    private static BookingEventEnvelope MakeDrawStarted(string eventId, string drawAttemptId) =>
        new(EventId: eventId,
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-1",
            CorrelationId: "corr-1",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-1",
                Date: "2026-06-10",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                DrawAttemptId: drawAttemptId));

    private static BookingEventEnvelope MakeDrawCompleted(string eventId, string drawAttemptId, string? causationId) =>
        new(EventId: eventId,
            EventType: "booking.drawCompleted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-1",
            CorrelationId: "corr-1",
            CausationId: causationId,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-1",
                Date: "2026-06-10",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                DrawAttemptId: drawAttemptId,
                AllocatedCount: 3,
                RejectedCount: 1,
                WaitlistedCount: 0));
}
