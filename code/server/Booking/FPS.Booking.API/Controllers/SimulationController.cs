using FPS.Booking.API.Simulation;
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
    ICurrentUser currentUser) : ControllerBase
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
    public IActionResult Advance([FromBody] AdvanceRequest body)
    {
        if (env.IsProduction()) return NotFound();
        if (body.Hours < 1 || body.Hours > 744)
            return BadRequest("Hours must be between 1 and 744.");
        var sim = clock as InMemorySimulationClock;
        if (sim is null) return StatusCode(501, "Simulation clock not registered.");
        sim.Advance(currentUser.TenantId, TimeSpan.FromHours(body.Hours));
        return Ok(BuildResponse(sim, currentUser.TenantId));
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
