using FPS.Reporting.Application;
using FPS.Reporting.Infrastructure;

namespace FPS.Reporting.Tests;

public sealed class BookingEventReportingHandlerTests
{
    private readonly InMemoryReportingRepository repository = new();
    private readonly BookingEventReportingHandler handler;

    public BookingEventReportingHandlerTests()
    {
        handler = new BookingEventReportingHandler(repository);
    }

    [Fact]
    public async Task Handle_RequestSubmitted_IncrementsDemandAndFairnessRequest()
    {
        await handler.HandleAsync(Envelope("evt-1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var metrics = await repository.QueryMetricsAsync(new(), "t1");
        Assert.Single(metrics);
        Assert.Equal(1, metrics[0].DemandCount);
        Assert.Equal(0, metrics[0].AllocationCount);

        var fairness = await repository.QueryFairnessAsync(new(), "t1");
        Assert.Single(fairness);
        Assert.Equal(1, fairness[0].RequestCount);
        Assert.Equal(0, fairness[0].AllocationCount);
    }

    [Fact]
    public async Task Handle_SlotAllocated_IncrementsAllocationAndFairnessAllocation()
    {
        await handler.HandleAsync(Envelope("evt-1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("evt-2", "booking.slotAllocated", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var metrics = await repository.QueryMetricsAsync(new(), "t1");
        Assert.Equal(1, metrics[0].DemandCount);
        Assert.Equal(1, metrics[0].AllocationCount);
        Assert.Equal(1.0, metrics[0].AllocationRate);

        var fairness = await repository.QueryFairnessAsync(new(), "t1");
        Assert.Equal(1, fairness[0].RequestCount);
        Assert.Equal(1, fairness[0].AllocationCount);
    }

    [Fact]
    public async Task Handle_RequestRejected_IncrementsRejectionCountAndByReason()
    {
        await handler.HandleAsync(Envelope("evt-1", "booking.requestRejected", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00", reasonCode: "noCapacity"));

        var metrics = await repository.QueryMetricsAsync(new(), "t1");
        Assert.Equal(1, metrics[0].RejectionCount);
        Assert.Equal(1, metrics[0].RejectionByReason["noCapacity"]);
    }

    [Fact]
    public async Task Handle_DuplicateEvent_DoesNotDoubleCount()
    {
        await handler.HandleAsync(Envelope("evt-1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("evt-1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var metrics = await repository.QueryMetricsAsync(new(), "t1");
        Assert.Equal(1, metrics[0].DemandCount);
    }

    [Fact]
    public async Task Handle_TenantIsolation_MetricsArePerTenant()
    {
        await handler.HandleAsync(Envelope("evt-1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("evt-2", "booking.requestSubmitted", "t2", "u2", "loc-1", "2026-06-01", "09:00-17:00"));

        var t1 = await repository.QueryMetricsAsync(new(), "t1");
        var t2 = await repository.QueryMetricsAsync(new(), "t2");

        Assert.Single(t1);
        Assert.Single(t2);
        Assert.Equal("t1", t1[0].TenantId);
        Assert.Equal("t2", t2[0].TenantId);
    }

    [Fact]
    public async Task Handle_FairnessIsolation_RecordsArePerTenant()
    {
        await handler.HandleAsync(Envelope("evt-1", "booking.requestSubmitted", "t1", "same-user-id", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("evt-2", "booking.requestSubmitted", "t2", "same-user-id", "loc-1", "2026-06-01", "09:00-17:00"));

        var t1 = await repository.QueryFairnessAsync(new(), "t1");
        var t2 = await repository.QueryFairnessAsync(new(), "t2");

        Assert.Single(t1);
        Assert.Single(t2);
    }

    [Fact]
    public async Task Handle_RequestSubmitted_FairnessUsesHashedId()
    {
        await handler.HandleAsync(Envelope("evt-1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var fairness = await repository.QueryFairnessAsync(new(), "t1");
        Assert.NotEqual("u1", fairness[0].RequestorHash);
        Assert.Equal(BookingEventReportingHandler.Hash("u1"), fairness[0].RequestorHash);
    }

    [Fact]
    public async Task Handle_CancellationNoShowPenaltyUsageConfirmed_CountsAreCorrect()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestCancelled", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.noShowRecorded", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e3", "booking.penaltyApplied", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e4", "booking.usageConfirmed", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var metrics = await repository.QueryMetricsAsync(new(), "t1");
        Assert.Equal(1, metrics[0].CancellationCount);
        Assert.Equal(1, metrics[0].NoShowCount);
        Assert.Equal(1, metrics[0].PenaltyCount);
        Assert.Equal(1, metrics[0].UsageConfirmedCount);
    }

    [Fact]
    public async Task Handle_DrawCompletedAndManualCorrection_DoNotThrowOrCountMetrics()
    {
        await handler.HandleAsync(Envelope("e1", "booking.drawCompleted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.manualCorrectionApplied", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var metrics = await repository.QueryMetricsAsync(new(), "t1");
        Assert.Empty(metrics);
    }

    private static BookingEventEnvelope Envelope(
        string eventId, string eventType, string tenantId, string requestorId,
        string locationId, string date, string timeSlot,
        string? reasonCode = null) => new(
        EventId: eventId,
        EventType: eventType,
        EventVersion: 1,
        OccurredAt: DateTime.UtcNow,
        TenantId: tenantId,
        CorrelationId: "corr-1",
        CausationId: null,
        ActorType: "employee",
        ActorId: requestorId,
        Source: "booking",
        Payload: new BookingEventPayload(
            BookingRequestId: "req-1",
            RequestorId: requestorId,
            LocationId: locationId,
            Date: date,
            TimeSlot: timeSlot,
            PreviousStatus: null,
            NewStatus: null,
            ReasonCode: reasonCode,
            ReasonText: null,
            AffectedRecipientIds: null));
}
