using FPS.Reporting.Application;
using FPS.Reporting.Domain;
using FPS.Reporting.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPS.Reporting.Tests;

public sealed class OperationalReportTests
{
    private readonly InMemoryReportingRepository repository = new();
    private readonly BookingEventReportingHandler handler;
    private readonly ReportingQueryService service;

    public OperationalReportTests()
    {
        handler = new BookingEventReportingHandler(repository, NullLogger<BookingEventReportingHandler>.Instance);
        service = new ReportingQueryService(repository);
    }

    private Task Submit(string eventId, string tenantId, string locationId, string date, string timeSlot = "09:00-17:00") =>
        handler.HandleAsync(new BookingEventEnvelope(
            EventId: eventId, EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: tenantId, CorrelationId: "c",
            CausationId: null, ActorType: "employee", ActorId: "user-1", Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: eventId, RequestorId: "user-1",
                LocationId: locationId, Date: date, TimeSlot: timeSlot,
                PreviousStatus: null, NewStatus: null,
                ReasonCode: null, ReasonText: null, AffectedRecipientIds: null)));

    private Task Reject(string eventId, string tenantId, string locationId, string date, string reason) =>
        handler.HandleAsync(new BookingEventEnvelope(
            EventId: eventId, EventType: "booking.requestRejected", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: tenantId, CorrelationId: "c",
            CausationId: null, ActorType: "system", ActorId: null, Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: eventId, RequestorId: "user-1",
                LocationId: locationId, Date: date, TimeSlot: "09:00-17:00",
                PreviousStatus: null, NewStatus: null,
                ReasonCode: reason, ReasonText: null, AffectedRecipientIds: null)));

    private Task Cancel(string eventId, string tenantId, string locationId, string date) =>
        handler.HandleAsync(new BookingEventEnvelope(
            EventId: eventId, EventType: "booking.requestCancelled", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: tenantId, CorrelationId: "c",
            CausationId: null, ActorType: "employee", ActorId: "user-1", Source: "booking",
            Payload: new BookingEventPayload(
                BookingRequestId: eventId, RequestorId: "user-1",
                LocationId: locationId, Date: date, TimeSlot: "09:00-17:00",
                PreviousStatus: null, NewStatus: null,
                ReasonCode: null, ReasonText: null, AffectedRecipientIds: null)));

    // ── Utilization ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Utilization_EmptyData_ReturnsEmptyList()
    {
        var result = await service.GetUtilizationAsync(new(), "tenant-1");

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Utilization_GroupsByLocation()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Submit("e2", "t1", "loc-A", "2026-06-02");
        await Submit("e3", "t1", "loc-B", "2026-06-01");

        var result = await service.GetUtilizationAsync(new(), "t1");

        Assert.Equal(2, result.Items.Count);
        var a = result.Items.First(x => x.LocationId == "loc-A");
        Assert.Equal(2, a.TotalDemand);
        var b = result.Items.First(x => x.LocationId == "loc-B");
        Assert.Equal(1, b.TotalDemand);
    }

    [Fact]
    public async Task Utilization_TenantIsolation_ExcludesOtherTenants()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Submit("e2", "t2", "loc-A", "2026-06-01");

        var result = await service.GetUtilizationAsync(new(), "t1");

        Assert.Single(result.Items);
        Assert.Equal(1, result.Items[0].TotalDemand);
    }

    [Fact]
    public async Task Utilization_AllocationRate_ComputedPerLocation()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Submit("e2", "t1", "loc-A", "2026-06-02");
        await handler.HandleAsync(new BookingEventEnvelope(
            "e3", "booking.slotAllocated", 1, DateTime.UtcNow, "t1", "c",
            null, "system", null, "booking",
            new("e1", "user-1", "loc-A", "2026-06-01", "09:00-17:00", null, null, null, null, null)));

        var result = await service.GetUtilizationAsync(new(), "t1");

        var loc = result.Items.Single();
        Assert.Equal(2, loc.TotalDemand);
        Assert.Equal(1, loc.TotalAllocations);
        Assert.Equal(0.5, loc.AllocationRate, precision: 6);
    }

    [Fact]
    public async Task Utilization_OrderedByLocationId()
    {
        await Submit("e1", "t1", "loc-Z", "2026-06-01");
        await Submit("e2", "t1", "loc-A", "2026-06-01");
        await Submit("e3", "t1", "loc-M", "2026-06-01");

        var result = await service.GetUtilizationAsync(new(), "t1");

        Assert.Equal(new[] { "loc-A", "loc-M", "loc-Z" }, result.Items.Select(x => x.LocationId));
    }

    // ── Reason-code report ──────────────────────────────────────────────────

    [Fact]
    public async Task ReasonCodeReport_EmptyData_ReturnsEmpty()
    {
        var result = await service.GetReasonCodeReportAsync(new(), "tenant-1");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalDemand);
    }

    [Fact]
    public async Task ReasonCodeReport_AggregatesRejectionReasonCodes()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Reject("e2", "t1", "loc-A", "2026-06-01", "cap_exceeded");
        await Submit("e3", "t1", "loc-A", "2026-06-02");
        await Reject("e4", "t1", "loc-A", "2026-06-02", "cap_exceeded");
        await Submit("e5", "t1", "loc-A", "2026-06-03");
        await Reject("e6", "t1", "loc-A", "2026-06-03", "policy_violation");

        var result = await service.GetReasonCodeReportAsync(new(), "t1");

        var capExceeded = result.Items.First(x => x.ReasonCode == "cap_exceeded");
        Assert.Equal(2, capExceeded.Count);
        var policy = result.Items.First(x => x.ReasonCode == "policy_violation");
        Assert.Equal(1, policy.Count);
    }

    [Fact]
    public async Task ReasonCodeReport_IncludesCancellationAndNoShow()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Cancel("e2", "t1", "loc-A", "2026-06-01");
        await Submit("e3", "t1", "loc-A", "2026-06-02");
        await handler.HandleAsync(new BookingEventEnvelope(
            "e4", "booking.noShowRecorded", 1, DateTime.UtcNow, "t1", "c",
            null, "system", null, "booking",
            new("e3", "user-1", "loc-A", "2026-06-02", "09:00-17:00", null, null, null, null, null)));

        var result = await service.GetReasonCodeReportAsync(new(), "t1");

        Assert.Contains(result.Items, x => x.ReasonCode == "cancellation" && x.Count == 1);
        Assert.Contains(result.Items, x => x.ReasonCode == "no_show" && x.Count == 1);
    }

    [Fact]
    public async Task ReasonCodeReport_TenantIsolation()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Reject("e2", "t1", "loc-A", "2026-06-01", "cap_exceeded");
        await Submit("e3", "t2", "loc-A", "2026-06-01");
        await Reject("e4", "t2", "loc-A", "2026-06-01", "other_reason");

        var result = await service.GetReasonCodeReportAsync(new(), "t1");

        Assert.DoesNotContain(result.Items, x => x.ReasonCode == "other_reason");
    }

    [Fact]
    public async Task ReasonCodeReport_RateOfDemand_ComputedFromTotalDemand()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Submit("e2", "t1", "loc-A", "2026-06-02");
        await Reject("e3", "t1", "loc-A", "2026-06-01", "cap_exceeded");

        var result = await service.GetReasonCodeReportAsync(new(), "t1");

        Assert.Equal(2, result.TotalDemand);
        var entry = result.Items.Single(x => x.ReasonCode == "cap_exceeded");
        Assert.Equal(0.5, entry.RateOfDemand, precision: 6);
    }

    // ── CSV hardening ────────────────────────────────────────────────────────

    [Fact]
    public void CsvEscape_FormulaInjection_PrefixesLeadingEqualsSign()
    {
        Assert.StartsWith("'", CsvExport.Escape("=SUM(A1:A10)"));
    }

    [Fact]
    public void CsvEscape_FormulaInjection_PrefixesLeadingPlus()
    {
        Assert.StartsWith("'", CsvExport.Escape("+1234"));
    }

    [Fact]
    public void CsvEscape_FormulaInjection_PrefixesLeadingMinus()
    {
        Assert.StartsWith("'", CsvExport.Escape("-DROP TABLE"));
    }

    [Fact]
    public void CsvEscape_FormulaInjection_PrefixesLeadingAt()
    {
        Assert.StartsWith("'", CsvExport.Escape("@SUM"));
    }

    [Fact]
    public void CsvEscape_FormulaInjection_PrefixesLeadingPipe()
    {
        Assert.StartsWith("'", CsvExport.Escape("|pipe"));
    }

    [Fact]
    public void CsvEscape_NormalValue_NotPrefixed()
    {
        Assert.Equal("loc-A", CsvExport.Escape("loc-A"));
    }

    [Fact]
    public void CsvEscape_ValueWithComma_Quoted()
    {
        var result = CsvExport.Escape("a,b");
        Assert.StartsWith("\"", result);
        Assert.EndsWith("\"", result);
    }

    [Fact]
    public void CsvEscape_ValueWithEmbeddedQuote_DoubledInOutput()
    {
        Assert.Contains("\"\"", CsvExport.Escape("say \"hello\""));
    }

    [Fact]
    public void CsvEscape_EmbeddedNewline_Replaced()
    {
        var result = CsvExport.Escape("line1\nline2");
        Assert.DoesNotContain("\n", result);
    }

    [Fact]
    public void CsvEscape_EmbeddedCarriageReturn_Replaced()
    {
        var result = CsvExport.Escape("line1\rline2");
        Assert.DoesNotContain("\r", result);
    }

    [Fact]
    public async Task AllocationOutcomesCsv_EmptyData_ReturnsHeaderOnly()
    {
        var csv = await service.GetAllocationOutcomesCsvAsync(new(), "tenant-1");

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.StartsWith("Date,LocationId,TimeSlot", lines[0]);
    }

    [Fact]
    public async Task AllocationOutcomesCsv_DeterministicColumns()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");

        var csv = await service.GetAllocationOutcomesCsvAsync(new(), "t1");
        var header = csv.Split('\n')[0].Trim();

        Assert.Equal("Date,LocationId,TimeSlot,Demand,Allocations,AllocationRate,Rejections,Cancellations,NoShows", header);
    }

    [Fact]
    public async Task AllocationOutcomesCsv_TenantIsolation()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Submit("e2", "t2", "loc-B", "2026-06-01");

        var csv = await service.GetAllocationOutcomesCsvAsync(new(), "t1");

        Assert.DoesNotContain("loc-B", csv);
    }

    [Fact]
    public async Task AllocationOutcomesCsv_DoesNotExposeEmployeeIdentifiers()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");

        var csv = await service.GetAllocationOutcomesCsvAsync(new(), "t1");

        // No raw user IDs should appear (requestorId "user-1" must not be in output).
        Assert.DoesNotContain("user-1", csv);
    }

    [Fact]
    public async Task SummaryAndDashboard_StillWork_AfterNewEndpointsAdded()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");

        var summary = await service.GetSummaryAsync(new(), "t1");
        var dashboard = await service.GetDashboardAsync(new(), "t1");

        Assert.Single(summary.Items);
        Assert.Equal(1, dashboard.TotalDemand);
    }

    // ── Operational exceptions ───────────────────────────────────────────────

    [Fact]
    public async Task OperationalExceptions_EmptyData_ReturnsEmpty()
    {
        var result = await service.GetOperationalExceptionsAsync(new(), "tenant-1");

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task OperationalExceptions_DemandWithNoAllocationsOrRejections_ReturnsException()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");

        var result = await service.GetOperationalExceptionsAsync(new(), "t1");

        Assert.Single(result.Items);
        Assert.Equal("demand_no_allocations", result.Items[0].ExceptionType);
        Assert.Equal("loc-A", result.Items[0].LocationId);
        Assert.Equal(1, result.Items[0].TotalDemand);
    }

    [Fact]
    public async Task OperationalExceptions_AllRequestsRejectedZeroAllocations_ReturnsException()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Reject("e2", "t1", "loc-A", "2026-06-01", "cap_exceeded");

        var result = await service.GetOperationalExceptionsAsync(new(), "t1");

        Assert.Single(result.Items);
        Assert.Equal("all_rejected", result.Items[0].ExceptionType);
        Assert.Equal(1, result.Items[0].TotalDemand);
        Assert.Equal(1, result.Items[0].TotalRejections);
    }

    [Fact]
    public async Task OperationalExceptions_PartialAllocations_NoException()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Submit("e2", "t1", "loc-A", "2026-06-01");
        await handler.HandleAsync(new BookingEventEnvelope(
            "e3", "booking.slotAllocated", 1, DateTime.UtcNow, "t1", "c",
            null, "system", null, "booking",
            new("e1", "user-1", "loc-A", "2026-06-01", "09:00-17:00", null, null, null, null, null)));
        await Reject("e4", "t1", "loc-A", "2026-06-01", "cap_exceeded");

        var result = await service.GetOperationalExceptionsAsync(new(), "t1");

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task OperationalExceptions_TenantIsolation_ExcludesOtherTenants()
    {
        await Submit("e1", "t1", "loc-A", "2026-06-01");
        await Submit("e2", "t2", "loc-B", "2026-06-01");

        var result = await service.GetOperationalExceptionsAsync(new(), "t1");

        Assert.All(result.Items, e => Assert.NotEqual("loc-B", e.LocationId));
    }
}
