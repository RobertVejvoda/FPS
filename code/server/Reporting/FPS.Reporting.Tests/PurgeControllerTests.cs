using FPS.Reporting.Controllers;
using FPS.Reporting.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Reporting.Tests;

public sealed class PurgeControllerTests
{
    private readonly InMemoryReportingRepository repository = new();
    private readonly PurgeController controller;

    public PurgeControllerTests()
    {
        controller = new PurgeController(new ReportingTenantStorePurger(repository));
    }

    [Fact]
    public async Task PurgeTenant_TenantWithNoData_ReturnsOkWithZeroCount()
    {
        // The purger now delegates to the in-memory repository; a tenant that holds no rows
        // yields a real count of 0 (idempotent no-op), not a hard-coded stub 0.
        var request = new TenantPurgeRequest("tenant-1", SandboxReset: false);

        var result = await controller.PurgeTenant(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal(new TenantPurgeResponse("reporting", 0), payload);
    }

    [Fact]
    public async Task PurgeTenant_TenantWithData_ReturnsOkWithRemovedRowCount()
    {
        // Seed one metric row + one fairness row for the tenant, then purge via the endpoint.
        await repository.ApplyMetricsAsync("tenant-1", "2026-06-01", "loc-1", "09:00-17:00", m => m.IncrementDemand());
        await repository.ApplyFairnessAsync("tenant-1", "user-1", "2026-06-01", "loc-1", f => f.IncrementRequest());
        var request = new TenantPurgeRequest("tenant-1", SandboxReset: true);

        var result = await controller.PurgeTenant(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal(new TenantPurgeResponse("reporting", 2), payload);
    }

    [Fact]
    public async Task PurgeTenant_InvalidTenantId_ReturnsBadRequest()
    {
        // TenantPurgeScope.For rejects a blank/invalid tenant id, which the controller maps to 400.
        var request = new TenantPurgeRequest(string.Empty, SandboxReset: false);

        var result = await controller.PurgeTenant(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
