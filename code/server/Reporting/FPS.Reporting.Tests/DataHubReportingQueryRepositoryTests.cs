using System.Net;
using System.Text;
using FPS.Reporting.Domain;
using FPS.Reporting.Infrastructure;
using Microsoft.AspNetCore.Http;
using Moq;

namespace FPS.Reporting.Tests;

// #763 — Reporting reads report data from DataHub's durable projections over a token-forwarding
// HttpClient. These tests prove the mapping and that report output comes from the DataHub response
// (i.e. it survives a Reporting restart because it is not process-memory state), and that the caller's
// Authorization header is forwarded so tenant/roles come from DataHub's own auth.
public sealed class DataHubReportingQueryRepositoryTests
{
    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (DataHubReportingQueryRepository repo, StubHandler handler) Build(string json, string? authHeader = "Bearer t")
    {
        var handler = new StubHandler(json);
        var accessor = new Mock<IHttpContextAccessor>();
        if (authHeader is not null)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["Authorization"] = authHeader;
            accessor.Setup(a => a.HttpContext).Returns(ctx);
        }
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://fairspot-datahub") };
        return (new DataHubReportingQueryRepository(client, accessor.Object), handler);
    }

    private const string OneDailyRow = """
        {"items":[{"date":"2026-06-10","locationId":"LOC-A","timeSlot":"08:00-17:00",
          "demand":5,"allocated":3,"rejected":1,"cancelled":1,"noShow":0,"waitlisted":0,"penalties":2,
          "allocationRate":60.0,"rejectionsByReason":{"capacity_full":1}}],
         "total":1,"page":1,"pageSize":100}
        """;

    [Fact]
    public async Task QueryMetrics_MapsDataHubDailyRow_IncludingReasonsAndPenalty()
    {
        var (repo, _) = Build(OneDailyRow);

        var rows = await repo.QueryMetricsAsync(new ReportingQueryRequest(), "tenant-1");

        var m = Assert.Single(rows);
        Assert.Equal("tenant-1", m.TenantId);
        Assert.Equal("2026-06-10", m.Date);
        Assert.Equal("LOC-A", m.LocationId);
        Assert.Equal("08:00-17:00", m.TimeSlot);
        Assert.Equal(5, m.DemandCount);
        Assert.Equal(3, m.AllocationCount);   // DataHub's Allocated ∪ Used ∪ NoShow definition
        Assert.Equal(1, m.RejectionCount);
        Assert.Equal(1, m.CancellationCount);
        Assert.Equal(2, m.PenaltyCount);      // from PR1's penalty projection
        Assert.Equal(1, m.RejectionByReason["capacity_full"]);
    }

    [Fact]
    public async Task QueryMetrics_ForwardsAuthorizationHeader_ToDataHub()
    {
        var (repo, handler) = Build(OneDailyRow, authHeader: "Bearer caller-jwt");

        await repo.QueryMetricsAsync(new ReportingQueryRequest(), "tenant-1");

        Assert.NotNull(handler.LastRequest);
        Assert.Contains("datahub/metrics/daily", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("Bearer caller-jwt", handler.LastRequest.Headers.GetValues("Authorization"));
    }

    [Fact]
    public async Task QueryMetrics_FiltersByTimeSlot_ClientSide()
    {
        var (repo, _) = Build(OneDailyRow);

        var matching = await repo.QueryMetricsAsync(new ReportingQueryRequest { TimeSlot = "08:00-17:00" }, "tenant-1");
        Assert.Single(matching);

        var other = await repo.QueryMetricsAsync(new ReportingQueryRequest { TimeSlot = "18:00-20:00" }, "tenant-1");
        Assert.Empty(other);
    }

    [Fact]
    public async Task QueryFairness_MapsEmployeeImpact()
    {
        const string json = """
            {"period":{"from":"2026-06-01","to":"2026-06-30"},
             "items":[{"requestorId":"u1","demand":4,"allocated":1,"allocationRate":25.0,"rejected":3,"cancelled":0,"noShow":0}],
             "total":1,"page":1,"pageSize":100}
            """;
        var (repo, handler) = Build(json);

        var rows = await repo.QueryFairnessAsync(new FairnessQueryRequest(), "tenant-1");

        var f = Assert.Single(rows);
        Assert.Equal("u1", f.RequestorRef);
        Assert.Equal(4, f.RequestCount);
        Assert.Equal(1, f.AllocationCount);
        Assert.Equal(3, f.RejectionCount);
        Assert.Contains("datahub/metrics/employee-impact", handler.LastRequest!.RequestUri!.ToString());
    }
}
