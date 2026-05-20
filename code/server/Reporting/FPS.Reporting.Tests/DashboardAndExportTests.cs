using FPS.Reporting.Application;
using FPS.Reporting.Domain;
using FPS.Reporting.Infrastructure;

namespace FPS.Reporting.Tests;

public sealed class DashboardAndExportTests
{
    private static InMemoryReportingRepository MakeRepo() => new();

    private static async Task<ReportingQueryService> ServiceWithEvents(
        InMemoryReportingRepository repo, params (string type, string date, string location, string? reason)[] events)
    {
        var handler = new BookingEventReportingHandler(repo);
        foreach (var (type, date, location, reason) in events)
        {
            await handler.HandleAsync(new BookingEventEnvelope(
                EventId: Guid.NewGuid().ToString(), EventType: type, EventVersion: 1,
                OccurredAt: DateTime.UtcNow, TenantId: "tenant-1", CorrelationId: "c",
                CausationId: null, ActorType: "employee", ActorId: "user-1", Source: "booking",
                Payload: new BookingEventPayload(
                    BookingRequestId: "req-1", RequestorId: "user-1",
                    LocationId: location, Date: date, TimeSlot: "09:00-17:00",
                    PreviousStatus: null, NewStatus: null,
                    ReasonCode: reason, ReasonText: null, AffectedRecipientIds: null)));
        }
        return new ReportingQueryService(repo);
    }

    // ── Dashboard: totals ────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_EmptyData_ReturnsZeros()
    {
        var service = new ReportingQueryService(MakeRepo());
        var result = await service.GetDashboardAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.Equal(0, result.TotalDemand);
        Assert.Equal(0, result.TotalAllocations);
        Assert.Equal(0.0, result.OverallAllocationRate);
        Assert.Empty(result.DailyTrend);
    }

    [Fact]
    public async Task Dashboard_AggregatesTotalsAcrossDates()
    {
        var repo = MakeRepo();
        var service = await ServiceWithEvents(repo,
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null),
            ("booking.requestSubmitted", "2026-05-02", "loc-1", null),
            ("booking.slotAllocated",    "2026-05-01", "loc-1", null),
            ("booking.requestRejected",  "2026-05-02", "loc-1", "no_matching_slot_type"));

        var result = await service.GetDashboardAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.Equal(2, result.TotalDemand);
        Assert.Equal(1, result.TotalAllocations);
        Assert.Equal(1, result.TotalRejections);
        Assert.Equal(0.5, result.OverallAllocationRate, precision: 10);
    }

    [Fact]
    public async Task Dashboard_RejectionsByReason_Aggregated()
    {
        var repo = MakeRepo();
        var service = await ServiceWithEvents(repo,
            ("booking.requestRejected", "2026-05-01", "loc-1", "no_matching_slot_type"),
            ("booking.requestRejected", "2026-05-02", "loc-1", "no_matching_slot_type"),
            ("booking.requestRejected", "2026-05-01", "loc-1", "daily_cap_exceeded"));

        var result = await service.GetDashboardAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.Equal(2, result.RejectionsByReason["no_matching_slot_type"]);
        Assert.Equal(1, result.RejectionsByReason["daily_cap_exceeded"]);
    }

    // ── Dashboard: daily trend ───────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_DailyTrend_OrderedByDate()
    {
        var repo = MakeRepo();
        var service = await ServiceWithEvents(repo,
            ("booking.requestSubmitted", "2026-05-03", "loc-1", null),
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null),
            ("booking.requestSubmitted", "2026-05-02", "loc-1", null));

        var result = await service.GetDashboardAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.Equal(3, result.DailyTrend.Count);
        Assert.Equal("2026-05-01", result.DailyTrend[0].Date);
        Assert.Equal("2026-05-02", result.DailyTrend[1].Date);
        Assert.Equal("2026-05-03", result.DailyTrend[2].Date);
    }

    [Fact]
    public async Task Dashboard_DailyTrend_RatePerDay()
    {
        var repo = MakeRepo();
        var service = await ServiceWithEvents(repo,
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null),
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null),
            ("booking.slotAllocated",    "2026-05-01", "loc-1", null));

        var result = await service.GetDashboardAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.Single(result.DailyTrend);
        Assert.Equal(2, result.DailyTrend[0].Demand);
        Assert.Equal(1, result.DailyTrend[0].Allocations);
        Assert.Equal(0.5, result.DailyTrend[0].AllocationRate, precision: 10);
    }

    // ── Dashboard: tenant isolation ──────────────────────────────────────────

    [Fact]
    public async Task Dashboard_TenantIsolated()
    {
        var repo = MakeRepo();
        var handler = new BookingEventReportingHandler(repo);
        await handler.HandleAsync(new BookingEventEnvelope(
            EventId: Guid.NewGuid().ToString(), EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-X", CorrelationId: "c",
            CausationId: null, ActorType: "employee", ActorId: "u", Source: "booking",
            Payload: new BookingEventPayload("r", "u", "loc-1", "2026-05-01", "09:00-17:00", null, null, null, null, null)));

        var service = new ReportingQueryService(repo);
        var result = await service.GetDashboardAsync(new ReportingQueryRequest(), "tenant-Y");

        Assert.Equal(0, result.TotalDemand);
    }

    // ── CSV export ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Csv_EmptyData_ReturnsHeaderOnly()
    {
        var service = new ReportingQueryService(MakeRepo());
        var csv = await service.GetSummaryCsvAsync(new ReportingQueryRequest(), "tenant-1");

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("Date", lines[0]);
        Assert.Contains("AllocationRate", lines[0]);
    }

    [Fact]
    public async Task Csv_ContainsDataRows()
    {
        var repo = MakeRepo();
        var service = await ServiceWithEvents(repo,
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null),
            ("booking.slotAllocated",    "2026-05-01", "loc-1", null));

        var csv = await service.GetSummaryCsvAsync(new ReportingQueryRequest(), "tenant-1");
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length >= 2);
        Assert.Contains("2026-05-01", lines[1]);
        Assert.Contains("loc-1", lines[1]);
    }

    [Fact]
    public async Task Csv_NoEmployeeIds_InOutput()
    {
        var repo = MakeRepo();
        var service = await ServiceWithEvents(repo,
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null));

        var csv = await service.GetSummaryCsvAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.DoesNotContain("user-1", csv);
    }

    [Fact]
    public async Task Csv_AllocationRate_FormattedAsDecimal()
    {
        var repo = MakeRepo();
        var service = await ServiceWithEvents(repo,
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null),
            ("booking.requestSubmitted", "2026-05-01", "loc-1", null),
            ("booking.slotAllocated",    "2026-05-01", "loc-1", null));

        var csv = await service.GetSummaryCsvAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.Contains("0.5000", csv);
    }

    [Fact]
    public async Task Csv_TenantIsolated()
    {
        var repo = MakeRepo();
        var handler = new BookingEventReportingHandler(repo);
        await handler.HandleAsync(new BookingEventEnvelope(
            EventId: Guid.NewGuid().ToString(), EventType: "booking.requestSubmitted", EventVersion: 1,
            OccurredAt: DateTime.UtcNow, TenantId: "tenant-A", CorrelationId: "c",
            CausationId: null, ActorType: "e", ActorId: "u", Source: "booking",
            Payload: new BookingEventPayload("r", "u", "loc-1", "2026-05-01", "09:00", null, null, null, null, null)));

        var service = new ReportingQueryService(repo);
        var csv = await service.GetSummaryCsvAsync(new ReportingQueryRequest(), "tenant-B");
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines); // header only
    }
}
