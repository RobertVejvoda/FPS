using FPS.Booking.API.Simulation;
using FPS.Booking.Application.Services;
using FPS.SharedKernel.Identity;
using FPS.SharedKernel.Time;
using Microsoft.AspNetCore.Authorization;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("simulation")]
[Authorize]
public sealed class SimulationController(
    ISystemClock clock,
    IWebHostEnvironment env,
    ICurrentUser currentUser,
    IDrawSchedulerService schedulerService,
    DrawSchedulerOptions schedulerOptions) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(SimulationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetStatus()
    {
        if (env.IsProduction()) return NotFound();
        var sim = clock as InMemorySimulationClock;
        var tenantId = currentUser.TenantId;
        return Ok(BuildResponse(sim, tenantId));
    }

    [HttpPost("advance")]
    [Authorize(Roles = "admin,hr_manager")]
    [ProducesResponseType(typeof(SimulationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Advance([FromBody] AdvanceRequest body, CancellationToken cancellationToken)
    {
        if (env.IsProduction()) return NotFound();
        if (body.Hours < 1 || body.Hours > 744)
            return BadRequest("Hours must be between 1 and 744.");
        var sim = clock as InMemorySimulationClock;
        if (sim is null) return StatusCode(501, "Simulation clock not registered.");

        var tenantId = currentUser.TenantId;
        var oldNow = sim.GetTenantUtcNow(tenantId);

        // Advance the clock
        sim.Advance(tenantId, TimeSpan.FromHours(body.Hours));

        var newNow = sim.GetTenantUtcNow(tenantId);

        // Detect and trigger due scheduled Draws crossed by the time advance
        await TriggerDueDrawsInRangeAsync(tenantId, oldNow, newNow, cancellationToken);

        return Ok(BuildResponse(sim, tenantId));
    }

    private async Task TriggerDueDrawsInRangeAsync(
        string tenantId, DateTimeOffset oldNow, DateTimeOffset newNow, CancellationToken cancellationToken)
    {
        if (!schedulerOptions.Enabled) return;

        // The scheduler runs daily and triggers draws for (today + TargetDateOffsetDays).
        // When simulation time advances, we need to detect all days that were crossed
        // and trigger their corresponding draws.

        var oldDate = DateOnly.FromDateTime(oldNow.UtcDateTime);
        var newDate = DateOnly.FromDateTime(newNow.UtcDateTime);

        // If we crossed into a new day, trigger the scheduler for each day crossed
        // The scheduler would have run at the start of each new day
        var currentDate = oldDate;
        while (currentDate < newDate)
        {
            currentDate = currentDate.AddDays(1);
            var targetDate = currentDate.AddDays(schedulerOptions.TargetDateOffsetDays);
            await schedulerService.TriggerDueDrawsAsync(targetDate, cancellationToken);
        }

        // Also check if we need to trigger for the current newDate
        // (in case we advanced within the same day but need to check)
        if (oldDate != newDate)
        {
            // Already handled above by the loop
        }
        else
        {
            // Stayed within the same day - no scheduled trigger crossed
        }
    }

    [HttpPost("reset")]
    [Authorize(Roles = "admin,hr_manager")]
    [ProducesResponseType(typeof(SimulationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Reset()
    {
        if (env.IsProduction()) return NotFound();
        var sim = clock as InMemorySimulationClock;
        if (sim is not null) sim.Reset(currentUser.TenantId);
        return Ok(BuildResponse(sim, currentUser.TenantId));
    }

    private static SimulationStatusResponse BuildResponse(InMemorySimulationClock? sim, string tenantId)
        => new(
            SimulationActive: sim?.IsTenantSimulating(tenantId) ?? false,
            VirtualNow: sim?.GetVirtualNow(tenantId)?.ToString("O"),
            RealNow: DateTimeOffset.UtcNow.ToString("O"));
}

public sealed record SimulationStatusResponse(bool SimulationActive, string? VirtualNow, string RealNow);
public sealed record AdvanceRequest(int Hours);
