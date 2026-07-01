using FPS.Audit.Controllers;
using FPS.Audit.Domain;
using FPS.Audit.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Audit.Tests;

public sealed class PurgeControllerTests
{
    private readonly Mock<IAuditRetentionRepository> repository = new();

    private PurgeController BuildController()
        => new(new AuditTenantStorePurger(repository.Object));

    [Fact]
    public async Task PurgeTenant_InvalidTenantId_Returns400()
    {
        var controller = BuildController();

        var result = await controller.PurgeTenant(new TenantPurgeRequest("  ", SandboxReset: true), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        repository.Verify(r => r.PurgeTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeTenant_SandboxReset_ReturnsOkWithCount()
    {
        repository
            .Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        var controller = BuildController();

        var result = await controller.PurgeTenant(new TenantPurgeRequest("demo", SandboxReset: true), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal("audit", body.Service);
        Assert.Equal(5, body.Count);
    }

    [Fact]
    public async Task PurgeTenant_WithoutSandboxReset_ReturnsZero_AndPurgesNothing()
    {
        var controller = BuildController();

        var result = await controller.PurgeTenant(new TenantPurgeRequest("demo", SandboxReset: false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal("audit", body.Service);
        Assert.Equal(0, body.Count);
        repository.Verify(r => r.PurgeTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
