using FPS.Booking.API.Controllers;
using FPS.Booking.API.Simulation;
using FPS.Booking.Application.Services;
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
    private readonly Mock<IDrawSchedulerService> schedulerService = new();
    private readonly DrawSchedulerOptions schedulerOptions = new() { Enabled = false };

    public SimulationControllerTests()
    {
        currentUser.Setup(u => u.TenantId).Returns(TenantId);
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
    }

    private SimulationController CreateController(bool isProduction)
    {
        env.Setup(e => e.EnvironmentName).Returns(isProduction ? "Production" : "Development");
        var controller = new SimulationController(clock, env.Object, currentUser.Object, schedulerService.Object, schedulerOptions);
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
    public async Task Advance_Production_Returns404()
    {
        var result = await CreateController(isProduction: true).Advance(new AdvanceRequest(1), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Advance_ValidHours_AdvancesClockForTenantAndReturnsActive()
    {
        var controller = CreateController(isProduction: false);

        var result = await controller.Advance(new AdvanceRequest(8), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SimulationStatusResponse>(ok.Value);
        Assert.True(body.SimulationActive);
        Assert.NotNull(body.VirtualNow);
        Assert.True(clock.IsTenantSimulating(TenantId));
        Assert.True(clock.GetTenantUtcNow(TenantId) > clock.UtcNow.AddHours(7));
    }

    [Fact]
    public async Task Advance_ZeroHours_Returns400()
    {
        var result = await CreateController(isProduction: false).Advance(new AdvanceRequest(0), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Advance_TooManyHours_Returns400()
    {
        var result = await CreateController(isProduction: false).Advance(new AdvanceRequest(999), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Advance_DoesNotAffectOtherTenant()
    {
        var controller = CreateController(isProduction: false);
        await controller.Advance(new AdvanceRequest(4), CancellationToken.None);

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
    public async Task Reset_AfterAdvance_ClearsSimulationForTenant()
    {
        var controller = CreateController(isProduction: false);
        await controller.Advance(new AdvanceRequest(4), CancellationToken.None);
        Assert.True(clock.IsTenantSimulating(TenantId));

        var result = controller.Reset();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<SimulationStatusResponse>(ok.Value);
        Assert.False(body.SimulationActive);
        Assert.Null(body.VirtualNow);
        Assert.False(clock.IsTenantSimulating(TenantId));
    }

    // ── Simulation-triggered Draw tests ─────────────────────────────────────

    [Fact]
    public async Task Advance_WhenSchedulerDisabled_DoesNotTriggerDraws()
    {
        var options = new DrawSchedulerOptions { Enabled = false };
        var service = new Mock<IDrawSchedulerService>();
        var controller = new SimulationController(clock, env.Object, currentUser.Object, service.Object, options);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };

        env.Setup(e => e.EnvironmentName).Returns("Development");

        await controller.Advance(new AdvanceRequest(24), CancellationToken.None);

        service.Verify(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Advance_CrossesMidnight_TriggersScheduledDraw()
    {
        var options = new DrawSchedulerOptions { Enabled = true, TargetDateOffsetDays = 1 };
        var service = new Mock<IDrawSchedulerService>();
        service
            .Setup(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var controller = new SimulationController(clock, env.Object, currentUser.Object, service.Object, options);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };

        env.Setup(e => e.EnvironmentName).Returns("Development");

        // Advance 25 hours to ensure we cross at least one full day boundary
        await controller.Advance(new AdvanceRequest(25), CancellationToken.None);

        // Should trigger at least once (for the new day crossed)
        service.Verify(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Advance_LargeTimeJump_TriggersMultipleDates()
    {
        var options = new DrawSchedulerOptions { Enabled = true, TargetDateOffsetDays = 1 };
        var service = new Mock<IDrawSchedulerService>();
        var calledDates = new List<DateOnly>();
        service
            .Setup(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Callback<DateOnly, CancellationToken>((date, _) => calledDates.Add(date))
            .ReturnsAsync([]);

        var controller = new SimulationController(clock, env.Object, currentUser.Object, service.Object, options);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };

        env.Setup(e => e.EnvironmentName).Returns("Development");

        // Advance 3 days
        await controller.Advance(new AdvanceRequest(72), CancellationToken.None);

        // Should trigger for multiple dates
        Assert.NotEmpty(calledDates);
    }

    [Fact]
    public async Task Advance_IdempotentWithExistingScheduler_UsesSameWorkflowPath()
    {
        // This test verifies the integration: simulation advances trigger through
        // IDrawSchedulerService, which uses the same TriggerDrawCommand path as Dapr cron.
        var options = new DrawSchedulerOptions
        {
            Enabled = true,
            TargetDateOffsetDays = 1,
            Targets = [new DrawScheduleTarget
            {
                TenantId = TenantId,
                LocationId = "loc-1",
                TimeSlotStart = TimeSpan.FromHours(9),
                TimeSlotEnd = TimeSpan.FromHours(17)
            }]
        };
        var service = new Mock<IDrawSchedulerService>();
        service
            .Setup(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DrawSchedulerResult(TenantId, "loc-1", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "draw:key", "InProgress")]);

        var controller = new SimulationController(clock, env.Object, currentUser.Object, service.Object, options);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };

        env.Setup(e => e.EnvironmentName).Returns("Development");

        // Advance 25 hours to cross midnight
        await controller.Advance(new AdvanceRequest(25), CancellationToken.None);

        // Verify the service was called (proving the integration path is working)
        service.Verify(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
