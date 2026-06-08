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
                LocationId: "loc-hq",
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
}
