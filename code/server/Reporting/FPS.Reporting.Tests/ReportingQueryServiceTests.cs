using FPS.Reporting.Application;
using FPS.Reporting.Infrastructure;

namespace FPS.Reporting.Tests;

public sealed class ReportingQueryServiceTests
{
    private readonly InMemoryReportingRepository repository = new();
    private readonly ReportingQueryService service;
    private readonly BookingEventReportingHandler handler;

    public ReportingQueryServiceTests()
    {
        service = new ReportingQueryService(repository);
        handler = new BookingEventReportingHandler(repository);
    }

    [Fact]
    public async Task GetSummary_NoFilter_ReturnsAllTenantMetrics()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-02", "09:00-17:00"));

        var result = await service.GetSummaryAsync(new(), "t1");

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetSummary_TenantIsolation_ExcludesOtherTenants()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t2", "u2", "loc-1", "2026-06-01", "09:00-17:00"));

        var result = await service.GetSummaryAsync(new(), "t1");

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetSummary_FilterByDateRange_ReturnsMatchingOnly()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-10", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e3", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-20", "09:00-17:00"));

        var result = await service.GetSummaryAsync(new() { DateFrom = "2026-06-05", DateTo = "2026-06-15" }, "t1");

        Assert.Single(result.Items);
        Assert.Equal("2026-06-10", result.Items[0].Date);
    }

    [Fact]
    public async Task GetSummary_FilterByLocation_ReturnsMatchingOnly()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-A", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t1", "u1", "loc-B", "2026-06-01", "09:00-17:00"));

        var result = await service.GetSummaryAsync(new() { LocationId = "loc-A" }, "t1");

        Assert.Single(result.Items);
        Assert.Equal("loc-A", result.Items[0].LocationId);
    }

    [Fact]
    public async Task GetSummary_ResponseShape_DoesNotContainRawUserId()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "user-secret-id", "loc-1", "2026-06-01", "09:00-17:00"));

        var result = await service.GetSummaryAsync(new(), "t1");
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.DoesNotContain("user-secret-id", json);
    }

    [Fact]
    public async Task GetFairness_ReturnsHashedReferences_NotRawIds()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "raw-user-id", "loc-1", "2026-06-01", "09:00-17:00"));

        var result = await service.GetFairnessAsync(new(), "t1");

        Assert.Single(result.Items);
        Assert.DoesNotContain(result.Items, f => f.RequestorHash == "raw-user-id");
        Assert.Equal(BookingEventReportingHandler.Hash("raw-user-id"), result.Items[0].RequestorHash);
    }

    [Fact]
    public async Task GetFairness_TenantIsolation_ExcludesOtherTenants()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t2", "u2", "loc-1", "2026-06-01", "09:00-17:00"));

        var result = await service.GetFairnessAsync(new(), "t1");

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetFairness_MultipleUsers_AllocationRateIsCorrect()
    {
        // u1: 2 requests, 1 allocation → 50%
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-02", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e3", "booking.slotAllocated", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var result = await service.GetFairnessAsync(new(), "t1");

        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].RequestCount);
        Assert.Equal(1, result.Items[0].AllocationCount);
        Assert.Equal(0.5, result.Items[0].AllocationRate);
    }

    [Fact]
    public async Task GetFairness_FilterByDateRange_ExcludesOutOfRangeRequests()
    {
        // May request is outside the June filter window; only June counts.
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-05-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e3", "booking.slotAllocated", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var result = await service.GetFairnessAsync(new() { DateFrom = "2026-06-01", DateTo = "2026-06-30" }, "t1");

        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].RequestCount);
        Assert.Equal(1, result.Items[0].AllocationCount);
        Assert.Equal(1.0, result.Items[0].AllocationRate);
    }

    [Fact]
    public async Task GetFairness_FilterByLocation_ExcludesOtherLocations()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-A", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t1", "u1", "loc-B", "2026-06-01", "09:00-17:00"));

        var result = await service.GetFairnessAsync(new() { LocationId = "loc-A" }, "t1");

        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].RequestCount);
    }

    [Fact]
    public async Task GetSummary_AllocationRateCalculation_IsCorrect()
    {
        await handler.HandleAsync(Envelope("e1", "booking.requestSubmitted", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e2", "booking.requestSubmitted", "t1", "u2", "loc-1", "2026-06-01", "09:00-17:00"));
        await handler.HandleAsync(Envelope("e3", "booking.slotAllocated", "t1", "u1", "loc-1", "2026-06-01", "09:00-17:00"));

        var result = await service.GetSummaryAsync(new(), "t1");

        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].DemandCount);
        Assert.Equal(1, result.Items[0].AllocationCount);
        Assert.Equal(0.5, result.Items[0].AllocationRate);
    }

    private static BookingEventEnvelope Envelope(
        string eventId, string eventType, string tenantId, string requestorId,
        string locationId, string date, string timeSlot) => new(
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
            ReasonCode: null,
            ReasonText: null,
            AffectedRecipientIds: null));
}
