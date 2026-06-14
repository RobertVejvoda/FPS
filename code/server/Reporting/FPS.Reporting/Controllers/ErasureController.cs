using FPS.Reporting.Application;
using FPS.Reporting.Domain;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Reporting.Controllers;

[ApiController]
[DaprInternalOnly]
public sealed class ErasureController(IReportingRepository repository) : ControllerBase
{
    [HttpPost("/erasure")]
    public async Task<IActionResult> Erase([FromBody] ServiceErasureInput input, CancellationToken ct)
    {
        // Reporting now stores the raw requestor reference (the same id Profile
        // and Booking use) instead of a SHA hash — see issue #474. Prefer the
        // explicit TargetUserId field, which Audit's ErasureWorkflow already
        // forwards. The legacy TargetActorHash is intentionally not retried
        // against the new shape because no rows in it were ever hashed; sending
        // only a hash would silently match nothing, which is the right
        // post-rename behaviour.
        var targetRef = !string.IsNullOrEmpty(input.TargetUserId)
            ? input.TargetUserId
            : input.TargetActorHash;

        if (string.IsNullOrEmpty(targetRef))
            return Ok(new ServiceErasureResult("reporting", "notApplicable", 0));

        var count = await repository.AnonymiseFairnessByRequestorRefAsync(
            input.TenantId, targetRef, ct);
        return Ok(new ServiceErasureResult("reporting",
            count > 0 ? "anonymised" : "notApplicable", count));
    }
}

public sealed record ServiceErasureInput(string ErasureRequestId, string TenantId, string TargetActorHash, string? TargetUserId = null);
public sealed record ServiceErasureResult(string Service, string Treatment, int AffectedCount, string? Note = null);
