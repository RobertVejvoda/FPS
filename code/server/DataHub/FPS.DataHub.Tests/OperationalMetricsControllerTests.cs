using FPS.DataHub.Controllers;
using FPS.DataHub.Domain;
using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace FPS.DataHub.Tests;

// FakeCurrentUser is already declared as file-scoped in ProjectionControllerTests.cs.
// We use a local duplicate here to keep the test class self-contained.
file sealed class MetricsFakeUser(string tenantId, string userId = "admin-1") : ICurrentUser
{
    public string TenantId { get; } = tenantId;
    public string UserId { get; } = userId;
    public IReadOnlyList<string> Roles => [];
    public bool IsAuthenticated => true;
    public string? DisplayName => null;
    public bool IsInRole(string role) => false;
}

public sealed class OperationalMetricsControllerTests : IDisposable
{
    private readonly DataHubDbContext _db;
    private readonly OperationalMetricsController _controller;
    private const string Tenant = "tenant-a";
    private const string OtherTenant = "tenant-b";
    private static readonly DateOnly Today = new(2026, 6, 24);

    public OperationalMetricsControllerTests()
    {
        var opts = new DbContextOptionsBuilder<DataHubDbContext>()
            .UseInMemoryDatabase($"MetricsTest_{Guid.NewGuid()}")
            .Options;
        _db = new DataHubDbContext(opts);
        _controller = new OperationalMetricsController(_db, new MetricsFakeUser(Tenant));
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ────────────────────────────────────────────────────────────────

    private BookingOutcomeProjection Outcome(
        string requestId,
        string tenantId,
        string requestorId,
        string status,
        DateOnly? date = null,
        string? locationId = null,
        string? reasonCode = null) => new()
    {
        BookingRequestId = requestId,
        TenantId         = tenantId,
        RequestorId      = requestorId,
        LocationId       = locationId ?? "LOC-A",
        Date             = date ?? Today,
        TimeSlot         = "08:00-18:00",
        FinalStatus      = status,
        ReasonCode       = reasonCode,
        LastUpdatedAt    = DateTimeOffset.UtcNow,
    };

    private DrawHistoryProjection Draw(
        string drawId,
        string tenantId,
        string status,
        DateOnly? date = null,
        string? locationId = null,
        int allocated = 0,
        int rejected = 0,
        int waitlisted = 0,
        string? failureReason = null) => new()
    {
        DrawAttemptId    = drawId,
        TenantId         = tenantId,
        LocationId       = locationId ?? "LOC-A",
        Date             = date ?? Today,
        TimeSlot         = "08:00-18:00",
        Status           = status,
        AllocatedCount   = allocated,
        RejectedCount    = rejected,
        WaitlistedCount  = waitlisted,
        SafeFailureReason = failureReason,
        LastUpdatedAt    = DateTimeOffset.UtcNow,
    };

    private async Task SaveAsync(params object[] entities)
    {
        _db.AddRange(entities);
        await _db.SaveChangesAsync();
    }

    // ── Dashboard ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_AggregatesStatusCounts()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated"),
            Outcome("r2", Tenant, "u1", "Allocated"),
            Outcome("r3", Tenant, "u2", "Rejected"),
            Outcome("r4", Tenant, "u3", "Cancelled"),
            Outcome("r5", Tenant, "u4", "Used"),
            Outcome("r6", Tenant, "u5", "NoShow"),
            Outcome("r7", Tenant, "u6", "Submitted"));

        var result = await _controller.Dashboard(null, Today, Today, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var resp = Assert.IsType<DashboardResponse>(ok.Value);
        Assert.Equal(7, resp.Demand);
        Assert.Equal(4, resp.Allocated); // Allocated(2) + Used(1) + NoShow(1)
        Assert.Equal(1, resp.Rejected);
        Assert.Equal(1, resp.Cancelled);
        Assert.Equal(1, resp.Used);
        Assert.Equal(1, resp.NoShow);
        Assert.Equal(1, resp.Submitted);
    }

    [Fact]
    public async Task Dashboard_AllocationRate_ComputedCorrectly()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated"),
            Outcome("r2", Tenant, "u1", "Allocated"),
            Outcome("r3", Tenant, "u2", "Rejected"),
            Outcome("r4", Tenant, "u3", "Rejected"));

        var result = await _controller.Dashboard(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<DashboardResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(50.0, resp.AllocationRate); // 2/4 = 50%
    }

    [Fact]
    public async Task Dashboard_EmptyData_ReturnsZeros()
    {
        var result = await _controller.Dashboard(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<DashboardResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(0, resp.Demand);
        Assert.Equal(0.0, resp.AllocationRate);
        Assert.Equal(0, resp.TotalDraws);
    }

    [Fact]
    public async Task Dashboard_TenantIsolation_ExcludesOtherTenantOutcomes()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated"),
            Outcome("r2", OtherTenant, "u2", "Allocated"),
            Outcome("r3", OtherTenant, "u3", "Allocated"));

        var result = await _controller.Dashboard(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<DashboardResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(1, resp.Demand);
    }

    [Fact]
    public async Task Dashboard_LocationFilter_LimitsResults()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated", locationId: "LOC-A"),
            Outcome("r2", Tenant, "u2", "Rejected",  locationId: "LOC-B"),
            Outcome("r3", Tenant, "u3", "Allocated", locationId: "LOC-B"));

        var result = await _controller.Dashboard("LOC-A", Today, Today, CancellationToken.None);

        var resp = Assert.IsType<DashboardResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(1, resp.Demand);
        Assert.Equal(1, resp.Allocated);
    }

    [Fact]
    public async Task Dashboard_IncludesDrawCounts()
    {
        await SaveAsync(
            Draw("d1", Tenant, "Completed", allocated: 5, rejected: 2),
            Draw("d2", Tenant, "Failed", failureReason: "timeout"),
            Draw("d3", Tenant, "Completed", allocated: 3));

        var result = await _controller.Dashboard(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<DashboardResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(3, resp.TotalDraws);
        Assert.Equal(1, resp.FailedDraws);
    }

    // ── Daily summary ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Daily_GroupsByDateLocationTimeslot()
    {
        var d1 = new DateOnly(2026, 6, 1);
        var d2 = new DateOnly(2026, 6, 2);
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated", date: d1, locationId: "LOC-A"),
            Outcome("r2", Tenant, "u2", "Rejected",  date: d1, locationId: "LOC-A"),
            Outcome("r3", Tenant, "u3", "Allocated", date: d2, locationId: "LOC-A"));

        var result = await _controller.Daily(null, d1, d2, page: 1, pageSize: 10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        // Two distinct date groups
        Assert.Contains("\"Total\":2", json);
    }

    [Fact]
    public async Task Daily_TenantIsolation()
    {
        await SaveAsync(
            Outcome("r1", Tenant,      "u1", "Allocated"),
            Outcome("r2", OtherTenant, "u2", "Allocated"));

        var result = await _controller.Daily(null, Today, Today, page: 1, pageSize: 10, CancellationToken.None);

        var ok   = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"Total\":1", json);
    }

    [Fact]
    public async Task Daily_AllocationRateComputedPerRow()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated"),
            Outcome("r2", Tenant, "u2", "Rejected"),
            Outcome("r3", Tenant, "u3", "Rejected"),
            Outcome("r4", Tenant, "u4", "Rejected"));

        var result = await _controller.Daily(null, Today, Today, page: 1, pageSize: 10, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("25", json); // 1/4 = 25%
    }

    // ── Utilization ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Utilization_GroupsByLocation()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated", locationId: "LOC-A"),
            Outcome("r2", Tenant, "u2", "Rejected",  locationId: "LOC-A"),
            Outcome("r3", Tenant, "u3", "Allocated", locationId: "LOC-B"),
            Outcome("r4", Tenant, "u4", "Allocated", locationId: "LOC-B"));

        var result = await _controller.Utilization(Today, Today, CancellationToken.None);

        var ok   = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("LOC-A", json);
        Assert.Contains("LOC-B", json);
    }

    [Fact]
    public async Task Utilization_UniqueRequestorsCounted()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "user-1", "Allocated"),
            Outcome("r2", Tenant, "user-1", "Rejected"), // same requestor
            Outcome("r3", Tenant, "user-2", "Allocated"));

        var result = await _controller.Utilization(Today, Today, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = ok.Value!.GetType().GetProperty("Items")!.GetValue(ok.Value) as IEnumerable<UtilizationRow>;
        var row = Assert.Single(items!);
        Assert.Equal(2, row.UniqueRequestors);
    }

    [Fact]
    public async Task Utilization_TenantIsolation()
    {
        await SaveAsync(
            Outcome("r1", Tenant,      "u1", "Allocated", locationId: "LOC-A"),
            Outcome("r2", OtherTenant, "u2", "Allocated", locationId: "LOC-A"));

        var result = await _controller.Utilization(Today, Today, CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result);
        var items = ok.Value!.GetType().GetProperty("Items")!.GetValue(ok.Value) as IEnumerable<UtilizationRow>;
        var row   = Assert.Single(items!);
        Assert.Equal(1, row.Demand);
    }

    // ── Reason codes ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReasonCodes_BucketsByStatus()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Rejected",   reasonCode: "capacity"),
            Outcome("r2", Tenant, "u2", "Rejected",   reasonCode: "capacity"),
            Outcome("r3", Tenant, "u3", "Cancelled",  reasonCode: "personal"),
            Outcome("r4", Tenant, "u4", "NoShow",     reasonCode: "undetected"),
            Outcome("r5", Tenant, "u5", "Allocated")); // no reason code, excluded

        var result = await _controller.ReasonCodes(null, Today, Today, CancellationToken.None);

        var ok   = Assert.IsType<OkObjectResult>(result);
        var resp = Assert.IsType<ReasonCodeResponse>(ok.Value);
        Assert.Single(resp.Rejections);
        Assert.Equal("capacity", resp.Rejections[0].ReasonCode);
        Assert.Equal(2, resp.Rejections[0].Count);
        Assert.Single(resp.Cancellations);
        Assert.Single(resp.NoShows);
    }

    [Fact]
    public async Task ReasonCodes_ExcludesNullReasonCode()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Rejected", reasonCode: null),
            Outcome("r2", Tenant, "u2", "Rejected", reasonCode: "capacity"));

        var result = await _controller.ReasonCodes(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<ReasonCodeResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(resp.Rejections);
    }

    [Fact]
    public async Task ReasonCodes_TenantIsolation()
    {
        await SaveAsync(
            Outcome("r1", Tenant,      "u1", "Rejected", reasonCode: "capacity"),
            Outcome("r2", OtherTenant, "u2", "Rejected", reasonCode: "capacity"));

        var result = await _controller.ReasonCodes(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<ReasonCodeResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(1, resp.Rejections[0].Count);
    }

    // ── Employee impact ────────────────────────────────────────────────────────

    [Fact]
    public async Task EmployeeImpact_GroupsByRequestor()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "user-1", "Allocated"),  // user-1: 1 demand
            Outcome("r2", Tenant, "user-2", "Allocated"),
            Outcome("r3", Tenant, "user-2", "Rejected"),
            Outcome("r4", Tenant, "user-2", "Cancelled")); // user-2: 3 demand

        var result = await _controller.EmployeeImpact(null, Today, Today, page: 1, pageSize: 10, CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result);
        var items = ok.Value!.GetType().GetProperty("Items")!.GetValue(ok.Value) as IEnumerable<EmployeeImpactRow>;
        var list  = items!.ToList();
        Assert.Equal(2, list.Count);
        // user-2 has more demand so should appear first (ordered by demand desc)
        Assert.Equal("user-2", list[0].RequestorId);
        Assert.Equal(3, list[0].Demand);
        Assert.Equal("user-1", list[1].RequestorId);
        Assert.Equal(1, list[1].Demand);
    }

    [Fact]
    public async Task EmployeeImpact_ExposesRequestorId()
    {
        await SaveAsync(Outcome("r1", Tenant, "employee-guid-123", "Allocated"));

        var result = await _controller.EmployeeImpact(null, Today, Today, page: 1, pageSize: 10, CancellationToken.None);

        var ok   = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("employee-guid-123", json);
    }

    [Fact]
    public async Task EmployeeImpact_TenantIsolation()
    {
        await SaveAsync(
            Outcome("r1", Tenant,      "user-1", "Allocated"),
            Outcome("r2", OtherTenant, "user-2", "Allocated"));

        var result = await _controller.EmployeeImpact(null, Today, Today, page: 1, pageSize: 10, CancellationToken.None);

        var ok   = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"Total\":1", json);
        Assert.DoesNotContain("user-2", json);
    }

    [Fact]
    public async Task EmployeeImpact_AllocationRatePerEmployee()
    {
        await SaveAsync(
            Outcome("r1", Tenant, "user-1", "Allocated"),
            Outcome("r2", Tenant, "user-1", "Allocated"),
            Outcome("r3", Tenant, "user-1", "Rejected"));

        var result = await _controller.EmployeeImpact(null, Today, Today, page: 1, pageSize: 10, CancellationToken.None);

        var ok    = Assert.IsType<OkObjectResult>(result);
        var items = ok.Value!.GetType().GetProperty("Items")!.GetValue(ok.Value) as IEnumerable<EmployeeImpactRow>;
        var row   = Assert.Single(items!);
        Assert.Equal(3, row.Demand);
        Assert.Equal(2, row.Allocated);
        Assert.InRange(row.AllocationRate, 66.6, 66.8);
    }

    // ── Operational exceptions ────────────────────────────────────────────────

    [Fact]
    public async Task OperationalExceptions_ReportsFailedDraws()
    {
        await SaveAsync(
            Draw("d1", Tenant, "Failed", failureReason: "timeout"),
            Draw("d2", Tenant, "Completed", allocated: 5));

        var result = await _controller.OperationalExceptions(null, Today, Today, CancellationToken.None);

        var ok   = Assert.IsType<OkObjectResult>(result);
        var resp = Assert.IsType<OperationalExceptionsResponse>(ok.Value);
        var failed = Assert.Single(resp.FailedDraws);
        Assert.Equal("d1", failed.DrawAttemptId);
        Assert.Equal("timeout", failed.SafeFailureReason);
    }

    [Fact]
    public async Task OperationalExceptions_ReportsZeroAllocationDraws()
    {
        await SaveAsync(
            Draw("d1", Tenant, "Completed", allocated: 0, rejected: 10),
            Draw("d2", Tenant, "Completed", allocated: 5, rejected: 2));

        var result = await _controller.OperationalExceptions(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<OperationalExceptionsResponse>(Assert.IsType<OkObjectResult>(result).Value);
        var zero = Assert.Single(resp.ZeroAllocationDraws);
        Assert.Equal("d1", zero.DrawAttemptId);
        Assert.Equal(10, zero.RejectedCount);
    }

    [Fact]
    public async Task OperationalExceptions_TenantIsolation()
    {
        await SaveAsync(
            Draw("d1", Tenant,      "Failed", failureReason: "mine"),
            Draw("d2", OtherTenant, "Failed", failureReason: "theirs"));

        var result = await _controller.OperationalExceptions(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<OperationalExceptionsResponse>(Assert.IsType<OkObjectResult>(result).Value);
        var failed = Assert.Single(resp.FailedDraws);
        Assert.Equal("d1", failed.DrawAttemptId);
    }

    [Fact]
    public async Task OperationalExceptions_EmptyData_ReturnsEmptyLists()
    {
        var result = await _controller.OperationalExceptions(null, Today, Today, CancellationToken.None);

        var resp = Assert.IsType<OperationalExceptionsResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Empty(resp.FailedDraws);
        Assert.Empty(resp.ZeroAllocationDraws);
        Assert.Null(resp.ProjectionLagSeconds);
    }

    // ── Date range defaults ────────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_DateRangeFilter_ExcludesOutOfRange()
    {
        var inRange  = new DateOnly(2026, 6, 1);
        var outRange = new DateOnly(2026, 5, 1);
        await SaveAsync(
            Outcome("r1", Tenant, "u1", "Allocated", date: inRange),
            Outcome("r2", Tenant, "u2", "Allocated", date: outRange));

        var result = await _controller.Dashboard(null, inRange, inRange, CancellationToken.None);

        var resp = Assert.IsType<DashboardResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(1, resp.Demand);
    }
}

// ── Authorization attribute tests ─────────────────────────────────────────────
// Verifies role configuration on OperationalMetricsController without
// invoking auth middleware — consistent with SecurityIngestionGuardTests pattern.
public sealed class MetricsAuthorizationTests
{
    private static readonly Type ControllerType = typeof(OperationalMetricsController);

    private static string EffectiveRoles(string methodName)
    {
        // Class-level roles: any of these is sufficient to pass class-level gate.
        var classRoles = ControllerType
            .GetCustomAttribute<AuthorizeAttribute>()?.Roles ?? "";

        // Action-level roles: AND'd with class-level in ASP.NET Core.
        var method = ControllerType.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Instance);
        var actionRoles = method?
            .GetCustomAttribute<AuthorizeAttribute>()?.Roles ?? "";

        // If action has a restrictive override, the effective set is the action's roles
        // (intersection with class roles, but class roles are a superset).
        return actionRoles.Length > 0 ? actionRoles : classRoles;
    }

    [Theory]
    [InlineData("Dashboard")]
    [InlineData("Daily")]
    [InlineData("Utilization")]
    [InlineData("ReasonCodes")]
    [InlineData("EmployeeImpact")]
    [InlineData("OperationalExceptions")]
    public void AllReportSafeEndpoints_AllowReportViewer(string action)
    {
        // All six endpoints must match the existing Reporting role contract:
        // hr_manager, admin, and report_viewer. EmployeeImpact and OperationalExceptions
        // have equivalent endpoints in ReportingController already open to report_viewer.
        var roles = EffectiveRoles(action);
        Assert.Contains("report_viewer", roles);
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        var classAttr = ControllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAttr);
    }
}
