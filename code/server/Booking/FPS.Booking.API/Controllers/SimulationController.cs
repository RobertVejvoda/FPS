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
        foreach (var date in ComputeTriggerTargets(oldNow, newNow, schedulerOptions.DrawCutOffTime, schedulerOptions.TargetDateOffsetDays))
            await schedulerService.TriggerDueDrawsAsync(date, cancellationToken);
    }

    // Determines which draw target dates are triggered when virtual time advances from oldNow to newNow.
    // A draw fires when the configured cut-off moment (date + cutOffTime UTC) falls in (oldNow, newNow].
    internal static IReadOnlyList<DateOnly> ComputeTriggerTargets(
        DateTimeOffset oldNow, DateTimeOffset newNow, TimeSpan cutOffTime, int targetOffsetDays)
    {
        var results = new List<DateOnly>();
        var firstDate = DateOnly.FromDateTime(oldNow.UtcDateTime);
        var lastDate = DateOnly.FromDateTime(newNow.UtcDateTime);
        for (var d = firstDate; d <= lastDate; d = d.AddDays(1))
        {
            var cutOffOn = new DateTimeOffset(d.Year, d.Month, d.Day,
                cutOffTime.Hours, cutOffTime.Minutes, cutOffTime.Seconds, TimeSpan.Zero);
            if (oldNow < cutOffOn && newNow >= cutOffOn)
                results.Add(d.AddDays(targetOffsetDays));
        }
        return results;
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
