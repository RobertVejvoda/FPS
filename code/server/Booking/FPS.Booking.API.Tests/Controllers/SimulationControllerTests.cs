using FPS.Booking.API.Controllers;
using FPS.Booking.API.Simulation;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Booking.API.Tests.Controllers;

public sealed class SimulationControllerTests
{
    private const string TenantId = "tenant-test";

    private readonly InMemorySimulationClock clock = new();
    private readonly Mock<IWebHostEnvironment> env = new();
    private readonly Mock<ICurrentUser> currentUser = new();

    public SimulationControllerTests()
    {
        currentUser.Setup(u => u.TenantId).Returns(TenantId);
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
    }

    private SimulationController CreateController(bool isProduction)
    {
        env.Setup(e => e.EnvironmentName).Returns(isProduction ? "Production" : "Development");
        var controller = new SimulationController(clock, env.Object, currentUser.Object);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };
        return controller;
    }

    [Fact]
    public void GetStatus_Production_Returns404()
    {
        var result = CreateController(isProduction: true).GetStatus();
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetStatus_Development_ReturnsOkWithInactiveSimulation()
    {
        var result = CreateController(isProduction: false).GetStatus();
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SimulationStatusResponse>(ok.Value);
        Assert.False(body.SimulationActive);
        Assert.Null(body.VirtualNow);
        Assert.NotEmpty(body.RealNow);
    }

    [Fact]
    public void Advance_Production_Returns404()
    {
        var result = CreateController(isProduction: true).Advance(new AdvanceRequest(1));
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Advance_ValidHours_AdvancesClockForTenantAndReturnsActive()
    {
        var controller = CreateController(isProduction: false);

        var result = controller.Advance(new AdvanceRequest(8));

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SimulationStatusResponse>(ok.Value);
        Assert.True(body.SimulationActive);
        Assert.NotNull(body.VirtualNow);
        Assert.True(clock.IsTenantSimulating(TenantId));
        Assert.True(clock.GetTenantUtcNow(TenantId) > clock.UtcNow.AddHours(7));
    }

    [Fact]
    public void Advance_ZeroHours_Returns400()
    {
        var result = CreateController(isProduction: false).Advance(new AdvanceRequest(0));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Advance_TooManyHours_Returns400()
    {
        var result = CreateController(isProduction: false).Advance(new AdvanceRequest(999));
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Advance_DoesNotAffectOtherTenant()
    {
        var controller = CreateController(isProduction: false);
        controller.Advance(new AdvanceRequest(4));

        Assert.True(clock.IsTenantSimulating(TenantId));
        Assert.False(clock.IsTenantSimulating("other-tenant"));
    }

    [Fact]
    public void Reset_Production_Returns404()
    {
        var result = CreateController(isProduction: true).Reset();
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Reset_AfterAdvance_ClearsSimulationForTenant()
    {
        var controller = CreateController(isProduction: false);
        controller.Advance(new AdvanceRequest(4));
        Assert.True(clock.IsTenantSimulating(TenantId));

        var result = controller.Reset();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SimulationStatusResponse>(ok.Value);
        Assert.False(body.SimulationActive);
        Assert.Null(body.VirtualNow);
        Assert.False(clock.IsTenantSimulating(TenantId));
    }
}
