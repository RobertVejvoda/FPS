using FPS.DataHub.Application;
using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.DataHub.Tests;

public sealed class BookingProjectionHandlerTests : IDisposable
{
    private readonly DataHubDbContext _db;
    private readonly BookingProjectionHandler _handler;

    public BookingProjectionHandlerTests()
    {
        var options = new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase($"DataHubTest_{Guid.NewGuid()}")
            .Options;
        _db = new DataHubDbContext(options);
        _handler = new BookingProjectionHandler(_db, NullLogger<BookingProjectionHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task HandleDrawStarted_CreatesDrawHistoryProjection()
    {
        // Arrange
        var envelope = new BookingEventEnvelope(
            EventId: "evt-draw-started-123",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-123",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-10",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        // Act
        await _handler.HandleAsync(envelope, CancellationToken.None);

        // Assert
        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "evt-draw-started-123");
        Assert.NotNull(projection);
        Assert.Equal("tenant-a", projection.TenantId);
        Assert.Equal("loc-hq", projection.LocationId);
        Assert.Equal(new DateOnly(2026, 6, 10), projection.Date);
        Assert.Equal("08:00-17:00", projection.TimeSlot);
        Assert.Equal("Running", projection.Status);
        Assert.Equal("scheduled", projection.TriggerSource);
    }

    [Fact]
    public async Task HandleDrawStarted_ManualRun_CapturesReasonAndTriggeredBy()
    {
        // Issue #472: the draw history projection must record the HR-supplied
        // reason and the runner so the Past Draws table can surface them.
        var envelope = new BookingEventEnvelope(
            EventId: "evt-draw-manual-1",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-manual-1",
            CausationId: null,
            ActorType: "hr_manager",
            ActorId: "hr-alice-hash",
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: "manual",
                ReasonText: "Cut-off reached early — running now",
                AffectedRecipientIds: null,
                DrawAttemptId: "draw-manual-1"));

        await _handler.HandleAsync(envelope, CancellationToken.None);

        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "draw-manual-1");
        Assert.NotNull(projection);
        Assert.Equal("manual", projection.TriggerSource);
        Assert.Equal("Cut-off reached early — running now", projection.RunReason);
        Assert.Equal("hr-alice-hash", projection.TriggeredBy);
    }

    [Fact]
    public async Task HandleDrawStarted_ScheduledRun_DoesNotExposeTriggeredBy()
    {
        // Scheduled runs must identify the source without leaking an actor id;
        // TriggeredBy stays null and TriggerSource defaults to "scheduled" when
        // no ReasonCode is present (legacy event compatibility).
        var envelope = new BookingEventEnvelope(
            EventId: "evt-draw-scheduled-1",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-sched-1",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: "scheduled",
                ReasonText: null,
                AffectedRecipientIds: null,
                DrawAttemptId: "draw-sched-1"));

        await _handler.HandleAsync(envelope, CancellationToken.None);

        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "draw-sched-1");
        Assert.NotNull(projection);
        Assert.Equal("scheduled", projection.TriggerSource);
        Assert.Null(projection.RunReason);
        Assert.Null(projection.TriggeredBy);
    }

    [Fact]
    public async Task HandleDrawCompleted_BackfillsMetadataWhenStartedMissedIt()
    {
        // If drawStarted was processed before the schema change (no
        // ReasonCode/ReasonText), drawCompleted should backfill — never
        // overwriting an existing value.
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-draw-backfill-start",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-5),
            TenantId: "tenant-a",
            CorrelationId: "corr-bf",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                DrawAttemptId: "draw-bf-1")),
            CancellationToken.None);

        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-draw-backfill-complete",
            EventType: "booking.drawCompleted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-bf",
            CausationId: "evt-draw-backfill-start",
            ActorType: "hr_manager",
            ActorId: "hr-bob-hash",
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: "recovery",
                ReasonText: "Retry after partial allocator failure",
                AffectedRecipientIds: null,
                DrawAttemptId: "draw-bf-1",
                AllocatedCount: 3,
                RejectedCount: 1,
                WaitlistedCount: 0)),
            CancellationToken.None);

        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "draw-bf-1");
        Assert.NotNull(projection);
        Assert.Equal("recovery", projection.TriggerSource);
        Assert.Equal("Retry after partial allocator failure", projection.RunReason);
        Assert.Equal("hr-bob-hash", projection.TriggeredBy);
    }

    [Fact]
    public async Task HandleDrawCompleted_UpdatesDrawHistoryProjection()
    {
        // Arrange - create started projection first
        var startedEnvelope = new BookingEventEnvelope(
            EventId: "evt-draw-started-456",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-5),
            TenantId: "tenant-a",
            CorrelationId: "corr-456",
            CausationId: null,
            ActorType: "manual",
            ActorId: "admin-1",
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "",
                Date: "2026-06-11",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        await _handler.HandleAsync(startedEnvelope, CancellationToken.None);

        var completedEnvelope = new BookingEventEnvelope(
            EventId: "evt-draw-completed-456",
            EventType: "booking.drawCompleted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-456",
            CausationId: "evt-draw-started-456",
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-11",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                AllocatedCount: 15,
                RejectedCount: 5,
                WaitlistedCount: 2));

        // Act
        await _handler.HandleAsync(completedEnvelope, CancellationToken.None);

        // Assert
        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "evt-draw-started-456");
        Assert.NotNull(projection);
        Assert.Equal("Completed", projection.Status);
        Assert.Equal(15, projection.AllocatedCount);
        Assert.Equal(5, projection.RejectedCount);
        Assert.Equal(2, projection.WaitlistedCount);
        Assert.NotNull(projection.CompletedAt);
    }

    [Fact]
    public async Task HandleDrawCompleted_LinksUndecidedSubmittedOutcomesAsWaitlisted()
    {
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-submitted-waitlist",
            EventType: "booking.requestSubmitted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-10),
            TenantId: "tenant-a",
            CorrelationId: "corr-waitlist",
            CausationId: null,
            ActorType: "employee",
            ActorId: "emp-wait",
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: "req-waitlist",
                RequestorId: "emp-wait",
                LocationId: "loc-hq",
                Date: "2026-06-11",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: "Submitted",
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null)), CancellationToken.None);

        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-completed-waitlist",
            EventType: "booking.drawCompleted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-waitlist",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-11",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                DrawAttemptId: "draw:tenant-a:loc-hq:2026-06-11:0800")), CancellationToken.None);

        var outcome = await _db.BookingOutcomes.SingleAsync(b => b.BookingRequestId == "req-waitlist");
        Assert.Equal("draw:tenant-a:loc-hq:2026-06-11:0800", outcome.DrawAttemptId);
        Assert.Equal("loc-hq", outcome.LocationId);
        Assert.Equal("Waitlisted", outcome.FinalStatus);
        Assert.NotNull(outcome.DecidedAt);
    }

    [Fact]
    public async Task HandleDrawStarted_IdempotentOnDuplicate()
    {
        // Arrange
        var envelope = new BookingEventEnvelope(
            EventId: "evt-draw-dup",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-dup",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-12",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        // Act - handle twice
        await _handler.HandleAsync(envelope, CancellationToken.None);
        await _handler.HandleAsync(envelope, CancellationToken.None);

        // Assert - should have exactly one projection
        var projections = await _db.DrawHistory.Where(d => d.DrawAttemptId == "evt-draw-dup").ToListAsync();
        Assert.Single(projections);
    }

    // DRAW009 tests: lifecycle steps + drawFailed event handling

    [Fact]
    public async Task HandleDrawCompleted_PersistsLifecycleStepsJson()
    {
        // Arrange - create a started projection first
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-steps-started",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-5),
            TenantId: "tenant-steps",
            CorrelationId: "corr-steps",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-a",
                Date: "2026-06-15",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null)),
            CancellationToken.None);

        var steps = new List<DrawProgressStepEnvelope>
        {
            new("Scheduled", "Completed", "Draw acquired lock", DateTime.UtcNow.AddMinutes(-4)),
            new("RequestsLoaded", "Completed", "12 requests loaded", DateTime.UtcNow.AddMinutes(-3)),
            new("DecisionsPersisted", "Completed", "Decisions saved", DateTime.UtcNow.AddMinutes(-1)),
        };

        // Act
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-steps-completed",
            EventType: "booking.drawCompleted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-steps",
            CorrelationId: "corr-steps",
            CausationId: "evt-steps-started",
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-a",
                Date: "2026-06-15",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                AllocatedCount: 10,
                RejectedCount: 2,
                WaitlistedCount: 0,
                LifecycleSteps: steps)),
            CancellationToken.None);

        // Assert - LifecycleStepsJson is persisted and deserialises to expected steps
        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "evt-steps-started");
        Assert.NotNull(projection);
        Assert.Equal("Completed", projection.Status);
        Assert.NotNull(projection.LifecycleStepsJson);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<FPS.DataHub.Domain.DrawProgressStepProjection>>(
            projection.LifecycleStepsJson!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized!.Count);
        Assert.Equal("Scheduled", deserialized[0].StepName);
        Assert.Equal("Completed", deserialized[0].Status);
        Assert.Equal("12 requests loaded", deserialized[1].Summary);
        Assert.Equal("DecisionsPersisted", deserialized[2].StepName);
    }

    [Fact]
    public async Task HandleDrawFailed_CreatesProjectionWithFailedStatusAndSafeReason()
    {
        // Arrange - start envelope so we have a projection to update
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-fail-started",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-2),
            TenantId: "tenant-fail",
            CorrelationId: "corr-fail",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-b",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null)),
            CancellationToken.None);

        var failSteps = new List<DrawProgressStepEnvelope>
        {
            new("Scheduled", "Completed", null, DateTime.UtcNow.AddMinutes(-1)),
            new("DrawFailed", "Failed", "Allocation engine error", DateTime.UtcNow),
        };

        // Act - handle drawFailed event
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-fail-failed",
            EventType: "booking.drawFailed",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-fail",
            CorrelationId: "corr-fail",
            CausationId: "evt-fail-started",
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-b",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                LifecycleSteps: failSteps,
                SafeFailureReason: "Draw failed due to an internal error. Please retry.")),
            CancellationToken.None);

        // Assert
        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "evt-fail-started");
        Assert.NotNull(projection);
        Assert.Equal("Failed", projection.Status);
        Assert.Equal("Draw failed due to an internal error. Please retry.", projection.SafeFailureReason);
        Assert.NotNull(projection.LifecycleStepsJson);
        Assert.NotNull(projection.CompletedAt);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<FPS.DataHub.Domain.DrawProgressStepProjection>>(
            projection.LifecycleStepsJson!,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal("DrawFailed", deserialized[1].StepName);
        Assert.Equal("Failed", deserialized[1].Status);
    }

    [Fact]
    public async Task HandleDrawFailed_WithoutPriorStarted_CreatesNewProjection()
    {
        // Act - drawFailed can arrive without a preceding drawStarted (e.g. startup race)
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-fail-nostart",
            EventType: "booking.drawFailed",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-nostart",
            CorrelationId: "corr-nostart",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-c",
                Date: "2026-06-17",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                SafeFailureReason: "Workflow could not be started.")),
            CancellationToken.None);

        // Assert - projection is created with Failed status
        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "evt-fail-nostart");
        Assert.NotNull(projection);
        Assert.Equal("Failed", projection.Status);
        Assert.Equal("Workflow could not be started.", projection.SafeFailureReason);
        Assert.Null(projection.LifecycleStepsJson);
    }

    [Fact]
    public async Task HandleDrawFailed_IdempotentOnDuplicate()
    {
        // Arrange
        var envelope = new BookingEventEnvelope(
            EventId: "evt-fail-dup",
            EventType: "booking.drawFailed",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-idem",
            CorrelationId: "corr-idem",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-d",
                Date: "2026-06-18",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                SafeFailureReason: "Retry exceeded."));

        // Act - handle twice
        await _handler.HandleAsync(envelope, CancellationToken.None);
        await _handler.HandleAsync(envelope, CancellationToken.None);

        // Assert - exactly one projection with Failed status
        var projections = await _db.DrawHistory.Where(d => d.DrawAttemptId == "evt-fail-dup").ToListAsync();
        Assert.Single(projections);
        Assert.Equal("Failed", projections[0].Status);
    }

    [Fact]
    public async Task HandleDrawCompleted_LifecycleStepsNull_DoesNotSetStepsJson()
    {
        // Arrange
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-nosteps-started",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-2),
            TenantId: "tenant-nosteps",
            CorrelationId: "corr-nosteps",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-e",
                Date: "2026-06-19",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null)),
            CancellationToken.None);

        // Act - drawCompleted with no lifecycle steps (pre-DRAW009 event)
        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-nosteps-completed",
            EventType: "booking.drawCompleted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-nosteps",
            CorrelationId: "corr-nosteps",
            CausationId: "evt-nosteps-started",
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-e",
                Date: "2026-06-19",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                AllocatedCount: 5)),
            CancellationToken.None);

        // Assert - status is Completed but LifecycleStepsJson stays null
        var projection = await _db.DrawHistory.FirstOrDefaultAsync(d => d.DrawAttemptId == "evt-nosteps-started");
        Assert.NotNull(projection);
        Assert.Equal("Completed", projection.Status);
        Assert.Null(projection.LifecycleStepsJson);
    }

    [Fact]
    public async Task HandleRequestSubmitted_CreatesBookingOutcomeProjection()
    {
        // Arrange
        var envelope = new BookingEventEnvelope(
            EventId: "evt-req-submitted-789",
            EventType: "booking.requestSubmitted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-789",
            CausationId: null,
            ActorType: "employee",
            ActorId: "emp-1",
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: "req-789",
                RequestorId: "emp-1",
                LocationId: "loc-hq",
                Date: "2026-06-15",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: "Submitted",
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        // Act
        await _handler.HandleAsync(envelope, CancellationToken.None);

        // Assert
        var projection = await _db.BookingOutcomes.FirstOrDefaultAsync(b => b.BookingRequestId == "req-789");
        Assert.NotNull(projection);
        Assert.Equal("tenant-a", projection.TenantId);
        Assert.Equal("emp-1", projection.RequestorId);
        Assert.Equal("loc-hq", projection.LocationId);
        Assert.Equal("Submitted", projection.FinalStatus);
    }

    [Fact]
    public async Task HandleRequestAllocated_UpdatesBookingOutcomeProjection()
    {
        // Arrange - create submitted projection first
        var submittedEnvelope = new BookingEventEnvelope(
            EventId: "evt-req-submitted-abc",
            EventType: "booking.requestSubmitted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-10),
            TenantId: "tenant-a",
            CorrelationId: "corr-abc",
            CausationId: null,
            ActorType: "employee",
            ActorId: "emp-2",
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: "req-abc",
                RequestorId: "emp-2",
                LocationId: "loc-hq",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: "Submitted",
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        await _handler.HandleAsync(submittedEnvelope, CancellationToken.None);

        var allocatedEnvelope = new BookingEventEnvelope(
            EventId: "evt-req-allocated-abc",
            EventType: "booking.slotAllocated",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-abc",
            CausationId: "evt-draw-completed-xyz",
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: "req-abc",
                RequestorId: "emp-2",
                LocationId: "loc-hq",
                Date: "2026-06-16",
                TimeSlot: "08:00-17:00",
                PreviousStatus: "Pending",
                NewStatus: "Allocated",
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                AllocationId: "alloc-123",
                SlotId: "slot-456",
                AllocationSource: "draw",
                DrawAttemptId: "evt-draw-completed-xyz"));

        // Act
        await _handler.HandleAsync(allocatedEnvelope, CancellationToken.None);

        // Assert
        var projection = await _db.BookingOutcomes.FirstOrDefaultAsync(b => b.BookingRequestId == "req-abc");
        Assert.NotNull(projection);
        Assert.Equal("Allocated", projection.FinalStatus);
        Assert.Equal("alloc-123", projection.AllocationId);
        Assert.Equal("slot-456", projection.SlotId);
        Assert.Equal("draw", projection.AllocationSource);
        Assert.Equal("evt-draw-completed-xyz", projection.DrawAttemptId);
    }

    [Fact]
    public async Task HandleRequestRejected_UpdatesBookingOutcomeProjection()
    {
        // Arrange
        var submittedEnvelope = new BookingEventEnvelope(
            EventId: "evt-req-submitted-def",
            EventType: "booking.requestSubmitted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-10),
            TenantId: "tenant-a",
            CorrelationId: "corr-def",
            CausationId: null,
            ActorType: "employee",
            ActorId: "emp-3",
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: "req-def",
                RequestorId: "emp-3",
                LocationId: "loc-hq",
                Date: "2026-06-17",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: "Submitted",
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        await _handler.HandleAsync(submittedEnvelope, CancellationToken.None);

        var rejectedEnvelope = new BookingEventEnvelope(
            EventId: "evt-req-rejected-def",
            EventType: "booking.requestRejected",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-def",
            CausationId: "evt-draw-completed-xyz",
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: "req-def",
                RequestorId: "emp-3",
                LocationId: "loc-hq",
                Date: "2026-06-17",
                TimeSlot: "08:00-17:00",
                PreviousStatus: "Pending",
                NewStatus: "Rejected",
                ReasonCode: "InsufficientCapacity",
                ReasonText: "All slots were allocated to other requests",
                AffectedRecipientIds: null));

        // Act
        await _handler.HandleAsync(rejectedEnvelope, CancellationToken.None);

        // Assert
        var projection = await _db.BookingOutcomes.FirstOrDefaultAsync(b => b.BookingRequestId == "req-def");
        Assert.NotNull(projection);
        Assert.Equal("Rejected", projection.FinalStatus);
        Assert.Equal("InsufficientCapacity", projection.ReasonCode);
        Assert.Equal("All slots were allocated to other requests", projection.SafeReasonText);
    }

    [Fact]
    public async Task TenantIsolation_ProjectionsAreScopedToTenant()
    {
        // Arrange - create projections for two different tenants
        var tenantAEnvelope = new BookingEventEnvelope(
            EventId: "evt-tenant-a",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-a",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-hq",
                Date: "2026-06-20",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        var tenantBEnvelope = new BookingEventEnvelope(
            EventId: "evt-tenant-b",
            EventType: "booking.drawStarted",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-b",
            CorrelationId: "corr-b",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: null,
                RequestorId: null,
                LocationId: "loc-office",
                Date: "2026-06-20",
                TimeSlot: "08:00-17:00",
                PreviousStatus: null,
                NewStatus: null,
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null));

        // Act
        await _handler.HandleAsync(tenantAEnvelope, CancellationToken.None);
        await _handler.HandleAsync(tenantBEnvelope, CancellationToken.None);

        // Assert
        var tenantAProjections = await _db.DrawHistory.Where(d => d.TenantId == "tenant-a").ToListAsync();
        var tenantBProjections = await _db.DrawHistory.Where(d => d.TenantId == "tenant-b").ToListAsync();

        Assert.Single(tenantAProjections);
        Assert.Single(tenantBProjections);
        Assert.Equal("loc-hq", tenantAProjections[0].LocationId);
        Assert.Equal("loc-office", tenantBProjections[0].LocationId);
    }

    // ── PERSIST005 evidence: tenant-scoped projection rows ────────────────────
    // Confirms that TenantId is present on all projection entity types and that
    // rows from different tenants are physically separate (not shared).

    [Fact]
    public async Task DrawHistoryProjection_HasTenantId()
    {
        var envelope = new BookingEventEnvelope(
            EventId: "evt-tenant-scope-1", EventType: "booking.drawStarted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-persist005", CorrelationId: "c1",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(null, null, "loc-hq", "2026-07-15", "08:00-17:00", null, null, null, null, null));

        await _handler.HandleAsync(envelope, CancellationToken.None);

        var row = await _db.DrawHistory.SingleAsync(d => d.DrawAttemptId == "evt-tenant-scope-1");
        Assert.Equal("tenant-persist005", row.TenantId);
    }

    [Fact]
    public async Task BookingOutcomeProjection_HasTenantId()
    {
        var requestId = Guid.NewGuid().ToString();
        var envelope = new BookingEventEnvelope(
            EventId: "evt-tenant-scope-2", EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-persist005", CorrelationId: "c2",
            CausationId: null, ActorType: "employee", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: requestId, RequestorId: "user-persist005",
                LocationId: "loc-hq", Date: "2026-07-15", TimeSlot: "08:00-17:00",
                PreviousStatus: null, NewStatus: "Submitted",
                ReasonCode: null, ReasonText: null, AffectedRecipientIds: null));

        await _handler.HandleAsync(envelope, CancellationToken.None);

        var row = await _db.BookingOutcomes.SingleAsync(o => o.BookingRequestId == requestId);
        Assert.Equal("tenant-persist005", row.TenantId);
    }

    [Fact]
    public async Task ProjectionRows_DifferentTenants_ArePhysicallySeparate()
    {
        var reqA = Guid.NewGuid().ToString();
        var reqB = Guid.NewGuid().ToString();

        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-sep-a", EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-a", CorrelationId: "ca",
            CausationId: null, ActorType: "employee", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(reqA, "user-1", "loc-a", "2026-07-15", "08:00-17:00", null, "Submitted", null, null, null)),
            CancellationToken.None);

        await _handler.HandleAsync(new BookingEventEnvelope(
            EventId: "evt-sep-b", EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-b", CorrelationId: "cb",
            CausationId: null, ActorType: "employee", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(reqB, "user-1", "loc-b", "2026-07-15", "08:00-17:00", null, "Submitted", null, null, null)),
            CancellationToken.None);

        // Each tenant can only see their own rows
        var aRows = await _db.BookingOutcomes.Where(o => o.TenantId == "tenant-a").ToListAsync();
        var bRows = await _db.BookingOutcomes.Where(o => o.TenantId == "tenant-b").ToListAsync();
        Assert.Single(aRows);
        Assert.Single(bRows);
        Assert.Equal(reqA, aRows[0].BookingRequestId);
        Assert.Equal(reqB, bRows[0].BookingRequestId);
    }

    // ── DATAHUB004 #335: terminal lifecycle transitions ────────────────────────
    // Coverage for the authoritative Release 1 producer event names that were
    // previously untested at the handler level: usageConfirmed, noShowRecorded,
    // requestCancelled, and requestExpired. Each is update-only (it transitions an
    // existing outcome), so the test seeds a prior outcome via the real submitted
    // path first.
    //
    // Note: booking.requestExpired is projection-supported here but is NOT yet
    // emitted by the Booking producer in Release 1 (no expiry event publisher
    // exists). This test proves the projection is ready if/when Booking emits it.

    private Task SeedSubmittedAsync(string reqId, string tenant = "tenant-a") =>
        _handler.HandleAsync(new BookingEventEnvelope(
            EventId: $"evt-submitted-{reqId}", EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow.AddMinutes(-10), TenantId: tenant, CorrelationId: $"corr-{reqId}",
            CausationId: null, ActorType: "employee", ActorId: "emp-x", Source: "booking",
            Payload: new BookingEventPayload(reqId, "emp-x", "loc-hq", "2026-06-20", "08:00-17:00", null, "Submitted", null, null, null)),
            CancellationToken.None);

    private static BookingEventEnvelope TerminalEvent(string reqId, string eventType, string tenant = "tenant-a") =>
        new(EventId: $"evt-{eventType}-{reqId}", EventType: eventType, EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: tenant, CorrelationId: $"corr-{reqId}",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(reqId, "emp-x", "loc-hq", "2026-06-20", "08:00-17:00", "Allocated", null, "cutoff", "Cut-off passed", null));

    [Theory]
    [InlineData("booking.usageConfirmed", "Used")]
    [InlineData("booking.noShowRecorded", "NoShow")]
    [InlineData("booking.requestCancelled", "Cancelled")]
    [InlineData("booking.requestExpired", "Expired")]
    public async Task HandleTerminalEvent_TransitionsOutcomeToFinalStatus(string eventType, string expectedStatus)
    {
        var reqId = $"req-{expectedStatus.ToLowerInvariant()}";
        await SeedSubmittedAsync(reqId);

        await _handler.HandleAsync(TerminalEvent(reqId, eventType), CancellationToken.None);

        var projection = await _db.BookingOutcomes.FirstOrDefaultAsync(b => b.BookingRequestId == reqId);
        Assert.NotNull(projection);
        Assert.Equal(expectedStatus, projection!.FinalStatus);
        Assert.Equal("tenant-a", projection.TenantId);
    }

    [Theory]
    [InlineData("booking.usageConfirmed", "Used")]
    [InlineData("booking.noShowRecorded", "NoShow")]
    [InlineData("booking.requestCancelled", "Cancelled")]
    [InlineData("booking.requestExpired", "Expired")]
    public async Task HandleTerminalEvent_DuplicateDelivery_IsIdempotent(string eventType, string expectedStatus)
    {
        var reqId = $"req-dup-{expectedStatus.ToLowerInvariant()}";
        await SeedSubmittedAsync(reqId);

        var evt = TerminalEvent(reqId, eventType);
        await _handler.HandleAsync(evt, CancellationToken.None);
        await _handler.HandleAsync(evt, CancellationToken.None);

        var rows = await _db.BookingOutcomes.Where(b => b.BookingRequestId == reqId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(expectedStatus, rows[0].FinalStatus);
    }

    // PLAT-seats (#710) — a draw allocation event can be processed before the submitted event
    // created the projection; the fallback-created outcome must still record the resource type.
    [Fact]
    public async Task HandleRequestAllocated_BeforeSubmitted_PreservesSeatsResourceType()
    {
        var envelope = new BookingEventEnvelope(
            EventId: "evt-seat-alloc-first",
            EventType: "booking.slotAllocated",
            EventVersion: 1,
            OccurredAt: DateTime.UtcNow,
            TenantId: "tenant-a",
            CorrelationId: "corr-seat-1",
            CausationId: null,
            ActorType: "system",
            ActorId: null,
            Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: "req-seat-alloc",
                RequestorId: "user-1",
                LocationId: "GL-TEAMS",
                Date: "2026-07-08",
                TimeSlot: "08:00-18:00",
                PreviousStatus: null,
                NewStatus: "Allocated",
                ReasonCode: null,
                ReasonText: null,
                AffectedRecipientIds: null,
                AllocationId: "alloc-seat-1",
                SlotId: "HQ-TEAM-A-01",
                AllocationSource: "draw",
                ResourceType: "Seats"));

        await _handler.HandleAsync(envelope, CancellationToken.None);

        var outcome = await _db.BookingOutcomes.SingleAsync(b => b.BookingRequestId == "req-seat-alloc");
        Assert.Equal("Allocated", outcome.FinalStatus);
        Assert.Equal("Seats", outcome.ResourceType);
    }
}
