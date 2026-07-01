using FPS.Notification.Controllers;
using FPS.Notification.Tests.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Notification.Tests.Controllers;

public sealed class PurgeControllerTests
{
    [Fact]
    public async Task PurgeTenant_InvalidTenantId_ReturnsBadRequest()
    {
        var harness = new NotificationStoreHarness();
        var controller = new PurgeController(harness.Purger);

        var result = await controller.PurgeTenant(new TenantPurgeRequest("", SandboxReset: false), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PurgeTenant_ValidTenant_ReturnsOkWithServiceAndCount()
    {
        var harness = new NotificationStoreHarness();
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-1"));
        await harness.Notifications.SaveAsync(NotificationStoreHarness.MakeRecord("ten-1", "user-2"));
        var controller = new PurgeController(harness.Purger);

        var result = await controller.PurgeTenant(new TenantPurgeRequest("ten-1", SandboxReset: true), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal("notification", response.Service);
        Assert.Equal(2, response.Count);
    }
}
