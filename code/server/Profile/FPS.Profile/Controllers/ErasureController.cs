using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Profile.Controllers;

[ApiController]
[DaprInternalOnly]
public sealed class ErasureController : ControllerBase
{
    // Stub — Profile erasure requires durable store cross-partition delete.
    // Returns notApplicable so the workflow can proceed; full implementation deferred to profile storage phase.
    [HttpPost("/erasure")]
    public IActionResult Erase([FromBody] ServiceErasureInput input) =>
        Ok(new ServiceErasureResult("profile", "notApplicable", 0,
            "Profile erasure requires durable store — deferred to storage phase."));
}

public sealed record ServiceErasureInput(string ErasureRequestId, string TenantId, string TargetActorHash, string? TargetUserId = null);
public sealed record ServiceErasureResult(string Service, string Treatment, int AffectedCount, string? Note = null);
