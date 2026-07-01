using FPS.Reporting.Controllers;
using FPS.Reporting.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Reporting.Tests;

public sealed class PurgeControllerTests
{
    private readonly PurgeController controller = new(new ReportingTenantStorePurger());

    [Fact]
    public async Task PurgeTenant_ValidTenant_ReturnsOkWithZeroCount()
    {
        // Reporting is an in-memory evaluation stub with no durable per-tenant store, so the
        // purger reports 0 — it exists for evidence symmetry in the platform purge fan-out.
        var request = new TenantPurgeRequest("tenant-1", SandboxReset: false);

        var result = await controller.PurgeTenant(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal(new TenantPurgeResponse("reporting", 0), payload);
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
