using FPS.Booking.API.Simulation;
using FPS.SharedKernel.Time;
using Microsoft.AspNetCore.Authorization;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("simulation")]
[AllowAnonymous]
public sealed class SimulationController(ISystemClock clock, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(SimulationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetStatus()
    {
        if (env.IsProduction()) return NotFound();
        var sim = clock as InMemorySimulationClock;
        return Ok(BuildResponse(sim));
    }

    [HttpPost("advance")]
    [ProducesResponseType(typeof(SimulationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Advance([FromBody] AdvanceRequest body)
    {
        if (env.IsProduction()) return NotFound();
        if (body.Hours < 1 || body.Hours > 744)
            return BadRequest("Hours must be between 1 and 744.");
        var sim = clock as InMemorySimulationClock;
        if (sim is null) return StatusCode(501, "Simulation clock not registered.");
        sim.Advance(TimeSpan.FromHours(body.Hours));
        return Ok(BuildResponse(sim));
    }

    [HttpPost("reset")]
    [ProducesResponseType(typeof(SimulationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Reset()
    {
        if (env.IsProduction()) return NotFound();
        var sim = clock as InMemorySimulationClock;
        sim?.Reset();
        return Ok(BuildResponse(sim));
    }

    private static SimulationStatusResponse BuildResponse(InMemorySimulationClock? sim)
        => new(
            SimulationActive: sim?.IsSimulating ?? false,
            VirtualNow: sim?.IsSimulating == true ? sim.UtcNow.ToString("O") : null,
            RealNow: DateTimeOffset.UtcNow.ToString("O"));
}

public sealed record SimulationStatusResponse(bool SimulationActive, string? VirtualNow, string RealNow);
public sealed record AdvanceRequest(int Hours);
