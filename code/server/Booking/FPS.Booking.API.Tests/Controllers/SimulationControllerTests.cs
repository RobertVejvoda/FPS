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

    // ── ComputeTriggerTargets unit tests (pure, time-independent) ────────────

    [Fact]
    public void ComputeTriggerTargets_SameDayCutOffCrossed_ReturnsTriggerDate()
    {
        var oldNow = new DateTimeOffset(2026, 6, 5, 17, 0, 0, TimeSpan.Zero);
        var newNow = new DateTimeOffset(2026, 6, 5, 19, 0, 0, TimeSpan.Zero);

        var dates = SimulationController.ComputeTriggerTargets(oldNow, newNow, TimeSpan.FromHours(18), targetOffsetDays: 1);

        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 6, 6), dates[0]);
    }

    [Fact]
    public void ComputeTriggerTargets_SameDayCutOffNotCrossed_ReturnsEmpty()
    {
        var oldNow = new DateTimeOffset(2026, 6, 5, 14, 0, 0, TimeSpan.Zero);
        var newNow = new DateTimeOffset(2026, 6, 5, 17, 0, 0, TimeSpan.Zero);

        var dates = SimulationController.ComputeTriggerTargets(oldNow, newNow, TimeSpan.FromHours(18), targetOffsetDays: 1);

        Assert.Empty(dates);
    }

    [Fact]
    public void ComputeTriggerTargets_AdvanceAfterCutOff_SameDayNoTrigger()
    {
        var oldNow = new DateTimeOffset(2026, 6, 5, 19, 0, 0, TimeSpan.Zero);
        var newNow = new DateTimeOffset(2026, 6, 5, 21, 0, 0, TimeSpan.Zero);

        var dates = SimulationController.ComputeTriggerTargets(oldNow, newNow, TimeSpan.FromHours(18), targetOffsetDays: 1);

        Assert.Empty(dates);
    }

    [Fact]
    public void ComputeTriggerTargets_MultiDayCrossing_ReturnsAllTriggerDates()
    {
        // Range covers three 18:00 cut-offs: June 5, 6, and 7 → targets June 6, 7, 8
        var oldNow = new DateTimeOffset(2026, 6, 5, 17, 0, 0, TimeSpan.Zero);
        var newNow = new DateTimeOffset(2026, 6, 7, 19, 0, 0, TimeSpan.Zero);

        var dates = SimulationController.ComputeTriggerTargets(oldNow, newNow, TimeSpan.FromHours(18), targetOffsetDays: 1);

        Assert.Equal(3, dates.Count);
        Assert.Equal(new DateOnly(2026, 6, 6), dates[0]);
        Assert.Equal(new DateOnly(2026, 6, 7), dates[1]);
        Assert.Equal(new DateOnly(2026, 6, 8), dates[2]);
    }

    [Fact]
    public void ComputeTriggerTargets_TargetOffsetDays_AppliedToTriggerDate()
    {
        var oldNow = new DateTimeOffset(2026, 6, 5, 17, 0, 0, TimeSpan.Zero);
        var newNow = new DateTimeOffset(2026, 6, 5, 19, 0, 0, TimeSpan.Zero);

        var dates = SimulationController.ComputeTriggerTargets(oldNow, newNow, TimeSpan.FromHours(18), targetOffsetDays: 2);

        Assert.Single(dates);
        Assert.Equal(new DateOnly(2026, 6, 7), dates[0]);
    }

    // ── Simulation-triggered Draw integration tests ──────────────────────────

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
    public async Task Advance_CrossesCutOffTime_TriggersScheduledDraw()
    {
        var options = new DrawSchedulerOptions { Enabled = true, DrawCutOffTime = TimeSpan.FromHours(18), TargetDateOffsetDays = 1 };
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

        // 25-hour advance always crosses at least one 18:00 cut-off regardless of starting time
        await controller.Advance(new AdvanceRequest(25), CancellationToken.None);

        service.Verify(s => s.TriggerDueDrawsAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Advance_LargeTimeJump_TriggersMultipleDates()
    {
        var options = new DrawSchedulerOptions { Enabled = true, DrawCutOffTime = TimeSpan.FromHours(18), TargetDateOffsetDays = 1 };
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

        // 72 hours (3 days) always crosses at least two 18:00 cut-offs
        await controller.Advance(new AdvanceRequest(72), CancellationToken.None);

        Assert.True(calledDates.Count >= 2);
    }
}
