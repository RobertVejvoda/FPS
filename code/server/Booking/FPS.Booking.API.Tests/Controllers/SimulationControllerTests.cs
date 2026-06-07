using FPS.Booking.API.Controllers;
using FPS.Booking.API.Simulation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FPS.Booking.API.Tests.Controllers;

public sealed class SimulationControllerTests
{
    private readonly InMemorySimulationClock clock = new();
    private readonly Mock<IWebHostEnvironment> env = new();

    private SimulationController CreateController(bool isProduction)
    {
        env.Setup(e => e.EnvironmentName)
            .Returns(isProduction ? "Production" : "Development");
        var controller = new SimulationController(clock, env.Object);
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
    public void Advance_ValidHours_AdvancesClockAndReturnsActive()
    {
        var controller = CreateController(isProduction: false);
        var before = clock.UtcNow;

        var result = controller.Advance(new AdvanceRequest(8));

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SimulationStatusResponse>(ok.Value);
        Assert.True(body.SimulationActive);
        Assert.NotNull(body.VirtualNow);
        Assert.True(clock.UtcNow > before.AddHours(7));
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
    public void Reset_Production_Returns404()
    {
        var result = CreateController(isProduction: true).Reset();
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Reset_AfterAdvance_ClearsSimulation()
    {
        var controller = CreateController(isProduction: false);
        controller.Advance(new AdvanceRequest(4));
        Assert.True(clock.IsSimulating);

        var result = controller.Reset();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SimulationStatusResponse>(ok.Value);
        Assert.False(body.SimulationActive);
        Assert.Null(body.VirtualNow);
        Assert.False(clock.IsSimulating);
    }
}
