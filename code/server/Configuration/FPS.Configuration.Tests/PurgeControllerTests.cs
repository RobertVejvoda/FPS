using FPS.Configuration.Controllers;
using FPS.Configuration.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Configuration.Tests;

public sealed class PurgeControllerTests
{
    private static PurgeController BuildController(IConfigurationTenantPurger purger)
        => new(new ConfigurationTenantStorePurger(purger));

    [Fact]
    public async Task PurgeTenant_InvalidTenantId_ReturnsBadRequest()
    {
        var purger = new Mock<IConfigurationTenantPurger>(MockBehavior.Strict);
        var controller = BuildController(purger.Object);

        var result = await controller.PurgeTenant(new TenantPurgeRequest("  ", SandboxReset: true), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        // Never reaches the purger when the tenant id is rejected up front.
        purger.Verify(p => p.PurgeTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeTenant_ValidTenant_ReturnsOkWithServiceAndCount()
    {
        var purger = new Mock<IConfigurationTenantPurger>();
        purger.Setup(p => p.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()))
              .ReturnsAsync(7);
        var controller = BuildController(purger.Object);

        var result = await controller.PurgeTenant(new TenantPurgeRequest("demo", SandboxReset: false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal("configuration", response.Service);
        Assert.Equal(7, response.Count);
    }
}
