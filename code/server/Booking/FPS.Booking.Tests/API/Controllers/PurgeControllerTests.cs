using FPS.Booking.Application.Repositories;
using FPS.Booking.Controllers;
using FPS.Booking.Infrastructure;
using FPS.SharedKernel.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FPS.Booking.Tests.Controllers;

/// <summary>
/// Tests the internal destructive tenant-purge endpoint (PLAT003C): invalid tenant ids are
/// rejected before any deletion, and a valid purge returns the per-service removed count.
/// </summary>
public sealed class PurgeControllerTests
{
    private readonly Mock<IBookingQueryRepository> repository = new();
    private readonly Mock<IDrawRepository> drawRepository = new();
    private readonly Mock<ICorrectionAuditRepository> correctionAuditRepository = new();
    private readonly PurgeController controller;

    public PurgeControllerTests()
    {
        var purger = new BookingTenantStorePurger(
            repository.Object, drawRepository.Object, correctionAuditRepository.Object);
        controller = new PurgeController(purger);
    }

    [Fact]
    public async Task PurgeTenant_InvalidTenantId_Returns400AndPurgesNothing()
    {
        var result = await controller.PurgeTenant(
            new TenantPurgeRequest("", SandboxReset: false), CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        repository.Verify(r => r.PurgeTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeTenant_ValidTenant_ReturnsOkWithPurgeResponse()
    {
        repository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var result = await controller.PurgeTenant(
            new TenantPurgeRequest("demo", SandboxReset: false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal("booking", response.Service);
        Assert.Equal(7, response.Count);
    }

    [Fact]
    public async Task PurgeTenant_EmptyTenant_ReturnsOkWithZeroCount()
    {
        repository.Setup(r => r.PurgeTenantAsync("demo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await controller.PurgeTenant(
            new TenantPurgeRequest("demo", SandboxReset: false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TenantPurgeResponse>(ok.Value);
        Assert.Equal(0, response.Count);
    }
}
