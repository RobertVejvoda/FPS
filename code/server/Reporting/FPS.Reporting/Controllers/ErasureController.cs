using FPS.Reporting.Application;
using FPS.Reporting.Domain;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Reporting.Controllers;

[ApiController]
public sealed class ErasureController(IReportingRepository repository) : ControllerBase
{
    [HttpPost("/erasure")]
    public async Task<IActionResult> Erase([FromBody] ServiceErasureInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.TargetActorHash))
            return Ok(new ServiceErasureResult("reporting", "notApplicable", 0));

        var count = await repository.AnonymiseFairnessByActorHashAsync(
            input.TenantId, input.TargetActorHash, ct);
        return Ok(new ServiceErasureResult("reporting",
            count > 0 ? "anonymised" : "notApplicable", count));
    }
}

public sealed record ServiceErasureInput(string ErasureRequestId, string TenantId, string TargetActorHash, string? TargetUserId = null);
public sealed record ServiceErasureResult(string Service, string Treatment, int AffectedCount, string? Note = null);
