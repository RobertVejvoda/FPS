using System.Net.Http.Json;
using FPS.Reporting.Domain;
using Microsoft.AspNetCore.Http;

namespace FPS.Reporting.Infrastructure;

/// <summary>
/// #763 — Reporting is a read/export facade over DataHub's <b>durable</b> Postgres projections. Report
/// data is read from DataHub's <c>/datahub/metrics/*</c> endpoints (token-forwarded), never from process
/// memory, so report output survives a Reporting restart. Mirrors Booking's token-forwarding
/// <c>HttpProfileSnapshotService</c>: DataHub read endpoints are <c>[Authorize]</c> and derive tenant +
/// roles from the caller's JWT, so the inbound Authorization header is forwarded and tenant scope comes
/// from DataHub's own auth — this repository never trusts a caller-supplied tenant.
///
/// Semantics note: an "allocation" here follows DataHub's durable definition (FinalStatus ∈
/// Allocated ∪ Used ∪ NoShow), which can differ from the old in-memory event-counting projection.
/// </summary>
public sealed class DataHubReportingQueryRepository(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    : IReportingQueryRepository
{
    public async Task<IReadOnlyList<ParkingMetrics>> QueryMetricsAsync(
        ReportingQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await GetAllPagesAsync<DailyRow>(
            "datahub/metrics/daily", request.DateFrom, request.DateTo, request.LocationId, cancellationToken);

        return rows
            // DataHub's daily endpoint has no timeSlot filter param, so filter the slot dimension here.
            .Where(r => request.TimeSlot is null || r.TimeSlot == request.TimeSlot)
            .Select(r => ParkingMetrics.Project(
                tenantId, r.Date, r.LocationId, r.TimeSlot,
                demand: r.Demand,
                allocation: r.Allocated,
                rejection: r.Rejected,
                cancellation: r.Cancelled,
                noShow: r.NoShow,
                penalty: r.Penalties,
                rejectionByReason: r.RejectionsByReason))
            .ToList();
    }

    public async Task<IReadOnlyList<FairnessRecord>> QueryFairnessAsync(
        FairnessQueryRequest request, string tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await GetAllPagesAsync<EmployeeImpactRow>(
            "datahub/metrics/employee-impact", request.DateFrom, request.DateTo, request.LocationId, cancellationToken);

        return rows
            .Select(r => FairnessRecord.Aggregate(tenantId, r.RequestorId, r.Demand, r.Allocated, r.Rejected))
            .OrderByDescending(f => f.AllocationRate)
            .ToList();
    }

    private async Task<List<T>> GetAllPagesAsync<T>(
        string path, string? dateFrom, string? dateTo, string? locationId, CancellationToken ct)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();

        var all = new List<T>();
        const int pageSize = 100;
        for (var page = 1; ; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{path}?{BuildQuery(dateFrom, dateTo, locationId, page, pageSize)}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Add("Authorization", authHeader);

            using var response = await httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PagedResponse<T>>(cancellationToken: ct);

            var items = result?.Items ?? [];
            all.AddRange(items);
            // Stop when we've read everything or the last page was short.
            if (result is null || items.Count < pageSize || all.Count >= result.Total)
                break;
        }
        return all;
    }

    private static string BuildQuery(string? dateFrom, string? dateTo, string? locationId, int page, int pageSize)
    {
        var parts = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrEmpty(dateFrom)) parts.Add($"fromDate={Uri.EscapeDataString(dateFrom)}");
        if (!string.IsNullOrEmpty(dateTo)) parts.Add($"toDate={Uri.EscapeDataString(dateTo)}");
        if (!string.IsNullOrEmpty(locationId)) parts.Add($"locationId={Uri.EscapeDataString(locationId)}");
        return string.Join('&', parts);
    }

    // DataHub read-response shapes (subset consumed here), matched by JSON.
    private sealed record PagedResponse<T>(List<T>? Items, int Total, int Page, int PageSize);

    private sealed record DailyRow(
        string Date, string LocationId, string TimeSlot,
        int Demand, int Allocated, int Rejected, int Cancelled, int NoShow, int Waitlisted, int Penalties,
        double AllocationRate, Dictionary<string, int>? RejectionsByReason);

    private sealed record EmployeeImpactRow(
        string RequestorId, int Demand, int Allocated, double AllocationRate, int Rejected, int Cancelled, int NoShow);
}
