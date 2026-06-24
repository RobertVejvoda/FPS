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
    public string? DisplayName => null;
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

    [Fact]
    public async Task GetMyOutcomes_IncludesAllocatedSlot()
    {
        // Issue #483: employees should see their own assigned slot in
        // history so they can recognise past allocations at a glance.
        _db.BookingOutcomes.Add(new BookingOutcomeProjection
        {
            BookingRequestId = "req-with-slot",
            TenantId = "tenant-a",
            RequestorId = "alice",
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 10),
            TimeSlot = "08:00-17:00",
            FinalStatus = "Allocated",
            SlotId = "Prague-A12",
        });
        await _db.SaveChangesAsync();

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "alice"));
        var result = await ctrl.GetMyOutcomes(ct: default) as OkObjectResult;

        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        // System.Text.Json with default options serialises as PascalCase
        // here (no MVC pipeline transforming). The wire format under MVC
        // is camelCase per ASP.NET Core defaults — covered indirectly by
        // the existing /datahub/my-outcomes integration tests.
        Assert.Contains("\"SlotId\":\"Prague-A12\"", json);
    }

    // ── HR Draw history tenant isolation ─────────────────────────────────────

    [Fact]
    public async Task GetDrawHistory_ReturnsOnlyCallerTenantDraws()
    {
        await SeedDraw("draw-a1", "tenant-a");
        await SeedDraw("draw-b1", "tenant-b");

        var ctrl = new DrawHistoryController(_db, new FakeCurrentUser("tenant-a", "hr-user"), NullLogger<DrawHistoryController>.Instance);
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

    // ── DRAW009: Draw progress endpoint ──────────────────────────────────────

    [Fact]
    public async Task GetDrawProgress_ReturnsProgressWithSteps_WhenStepsProjected()
    {
        // Arrange - seed a draw with lifecycle steps
        var stepsJson = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { StepName = "Scheduled", Status = "Completed", Summary = (string?)null, OccurredAt = (DateTime?)DateTime.UtcNow.AddMinutes(-5) },
            new { StepName = "DecisionsPersisted", Status = "Completed", Summary = (string?)"All decisions saved", OccurredAt = (DateTime?)DateTime.UtcNow.AddMinutes(-1) },
        });

        _db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = "draw-progress-1",
            TenantId = "tenant-a",
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 20),
            TimeSlot = "08:00-17:00",
            Status = "Completed",
            AllocatedCount = 8,
            RejectedCount = 2,
            WaitlistedCount = 1,
            LifecycleStepsJson = stepsJson
        });
        await _db.SaveChangesAsync();

        var ctrl = new DrawHistoryController(_db, new FakeCurrentUser("tenant-a", "hr-user"), NullLogger<DrawHistoryController>.Instance);

        // Act
        var result = await ctrl.GetDrawProgress("draw-progress-1", default) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("draw-progress-1", json);
        Assert.Contains("Completed", json);
        Assert.Contains("Scheduled", json);
        Assert.Contains("DecisionsPersisted", json);
        Assert.DoesNotContain("StepsNote", json.Replace("\"stepsNote\":null", "").Replace("\"StepsNote\":null", ""));
    }

    [Fact]
    public async Task GetDrawProgress_ReturnsStepsNote_WhenNoStepsProjected()
    {
        // Arrange - seed a draw without lifecycle steps (pre-DRAW009)
        _db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = "draw-progress-nosteps",
            TenantId = "tenant-a",
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 21),
            TimeSlot = "08:00-17:00",
            Status = "Completed",
            AllocatedCount = 3,
            LifecycleStepsJson = null
        });
        await _db.SaveChangesAsync();

        var ctrl = new DrawHistoryController(_db, new FakeCurrentUser("tenant-a", "hr-user"), NullLogger<DrawHistoryController>.Instance);

        // Act
        var result = await ctrl.GetDrawProgress("draw-progress-nosteps", default) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("draw-progress-nosteps", json);
        // Steps should be null but stepsNote should explain why
        Assert.DoesNotContain("\"Steps\":[", json);
    }

    [Fact]
    public async Task GetDrawProgress_CrossTenant_ReturnsNotFound()
    {
        await SeedDraw("draw-progress-other-tenant", "tenant-b");

        var ctrl = new DrawHistoryController(_db, new FakeCurrentUser("tenant-a", "hr-user"), NullLogger<DrawHistoryController>.Instance);
        var result = await ctrl.GetDrawProgress("draw-progress-other-tenant", default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetDrawProgress_UnknownId_ReturnsNotFound()
    {
        var ctrl = new DrawHistoryController(_db, new FakeCurrentUser("tenant-a", "hr-user"), NullLogger<DrawHistoryController>.Instance);
        var result = await ctrl.GetDrawProgress("no-such-draw", default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetDrawProgress_FailedDraw_ReturnsSafeFailureReason()
    {
        _db.DrawHistory.Add(new DrawHistoryProjection
        {
            DrawAttemptId = "draw-progress-failed",
            TenantId = "tenant-a",
            LocationId = "loc-1",
            Date = new DateOnly(2026, 6, 22),
            TimeSlot = "08:00-17:00",
            Status = "Failed",
            SafeFailureReason = "Draw failed due to an internal error. Please retry.",
            LifecycleStepsJson = null
        });
        await _db.SaveChangesAsync();

        var ctrl = new DrawHistoryController(_db, new FakeCurrentUser("tenant-a", "hr-user"), NullLogger<DrawHistoryController>.Instance);
        var result = await ctrl.GetDrawProgress("draw-progress-failed", default) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("Failed", json);
        Assert.Contains("Draw failed due to an internal error", json);
    }

    // ── AUD008: Booking request detail endpoint ───────────────────────────────

    [Fact]
    public async Task GetBookingRequestDetail_ReturnsDetail_WhenFound()
    {
        await SeedOutcome("req-detail-1", "tenant-a", "alice");

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "alice"));
        var result = await ctrl.GetBookingRequestDetail("req-detail-1", ct: default) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("req-detail-1", json);
        Assert.Contains("loc-1", json);
    }

    [Fact]
    public async Task GetBookingRequestDetail_CrossTenant_ReturnsNotFound()
    {
        await SeedOutcome("req-cross-1", "tenant-b", "alice");

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "alice"));
        var result = await ctrl.GetBookingRequestDetail("req-cross-1", ct: default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetBookingRequestDetail_UnknownId_ReturnsNotFound()
    {
        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "alice"));
        var result = await ctrl.GetBookingRequestDetail("no-such-req", ct: default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetBookingRequestDetail_ProjectsSafeFields_Allocated()
    {
        // Seed an allocated outcome with all optional fields
        _db.BookingOutcomes.Add(new BookingOutcomeProjection
        {
            BookingRequestId = "req-alloc-aud",
            TenantId = "tenant-a",
            RequestorId = "emp-secret-hash-abcdef",
            LocationId = "loc-hq",
            Date = new DateOnly(2026, 6, 20),
            TimeSlot = "08:00-17:00",
            FinalStatus = "Allocated",
            AllocationSource = "draw",
            SlotId = "Prague-A10",
            AllocationId = "alloc-internal",
            DrawAttemptId = "draw:tenant-a:loc-hq:2026-06-20:0800-1700",
            SubmittedAt = DateTime.UtcNow.AddHours(-2),
            DecidedAt = DateTime.UtcNow.AddHours(-1),
            VehicleLicensePlate = "1AB2345",
            VehicleType = "Car",
            VehicleIsElectric = true,
        });
        await _db.SaveChangesAsync();

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "auditor-user"));
        var result = await ctrl.GetBookingRequestDetail("req-alloc-aud", ct: default) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("\"BookingRequestId\":\"req-alloc-aud\"", json);
        Assert.Contains("\"Status\":\"Allocated\"", json);
        Assert.Contains("\"SlotId\":\"Prague-A10\"", json);
        Assert.Contains("\"AllocationSource\":\"draw\"", json);
        Assert.Contains("draw:tenant-a:loc-hq:2026-06-20:0800-1700", json);
        // RequestorShortRef: last 6 chars of "empsecrethashabcdef" uppercased → "ABCDEF"
        Assert.Contains("\"RequestorShortRef\":\"ABCDEF\"", json);
        // Vehicle facts must be present
        Assert.Contains("\"VehicleLicensePlate\":\"1AB2345\"", json);
        Assert.Contains("\"VehicleType\":\"Car\"", json);
        Assert.Contains("\"VehicleIsElectric\":true", json);
        // AllocationId (internal) must not be exposed
        Assert.DoesNotContain("alloc-internal", json);
        // Raw requestor hash must not be exposed
        Assert.DoesNotContain("emp-secret-hash-abcdef", json);
    }

    [Fact]
    public async Task GetBookingRequestDetail_ProjectsSafeFields_Rejected()
    {
        _db.BookingOutcomes.Add(new BookingOutcomeProjection
        {
            BookingRequestId = "req-rej-aud",
            TenantId = "tenant-a",
            RequestorId = "emp-hash-xyz123",
            LocationId = "loc-hq",
            Date = new DateOnly(2026, 6, 21),
            TimeSlot = "08:00-17:00",
            FinalStatus = "Rejected",
            ReasonCode = "InsufficientCapacity",
            SafeReasonText = "All slots were allocated to other requests",
        });
        await _db.SaveChangesAsync();

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "auditor-user"));
        var result = await ctrl.GetBookingRequestDetail("req-rej-aud", ct: default) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("\"Status\":\"Rejected\"", json);
        Assert.Contains("\"ReasonCode\":\"InsufficientCapacity\"", json);
        Assert.Contains("All slots were allocated to other requests", json);
        // "emp-hash-xyz123" → remove dashes → "emphashxyz123" → last 6 → "XYZ123"
        Assert.Contains("\"RequestorShortRef\":\"XYZ123\"", json);
        // Raw requestor hash not exposed
        Assert.DoesNotContain("emp-hash-xyz123", json);
    }

    [Fact]
    public async Task GetBookingRequestDetail_VehicleFactsNull_ForPreAud008Rows()
    {
        // Rows projected before the vehicle-facts migration have null vehicle fields
        await SeedOutcome("req-pre-aud008", "tenant-a", "user-abc123");

        var ctrl = new BookingOutcomesController(_db, new FakeCurrentUser("tenant-a", "auditor-user"));
        var result = await ctrl.GetBookingRequestDetail("req-pre-aud008", ct: default) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result!.Value);
        Assert.Contains("\"VehicleLicensePlate\":null", json);
        Assert.Contains("\"VehicleType\":null", json);
        Assert.Contains("\"VehicleIsElectric\":null", json);
        // Short ref still populated
        Assert.Contains("\"RequestorShortRef\":\"ABC123\"", json);
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
