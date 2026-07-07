using FPS.DataHub.Infrastructure;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.DataHub.Controllers;

/// <summary>
/// Service-owned user-level GDPR erasure endpoint (#772) for DataHub's durable report projections.
/// Called by the PRIV001 ErasureWorkflow (Audit) via Dapr service invocation as the "reporting
/// anonymisation" step — since #763 the durable report data lives here, not in Reporting. Protected
/// by DaprInternalOnly: requires the dapr-api-token header matching APP_API_TOKEN, so external
/// callers cannot reach it in production.
/// </summary>
[ApiController]
[DaprInternalOnly]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ErasureController(DataHubSubjectEraser eraser, ILogger<ErasureController> logger) : ControllerBase
{
    [HttpPost("/erasure")]
    public async Task<IActionResult> Erase([FromBody] DataHubErasureInput input, CancellationToken ct)
    {
        // DataHub stores the raw requestor reference (the same id Booking/Profile use, not a hash),
        // so prefer TargetUserId. The legacy TargetActorHash is intentionally not retried against the
        // raw shape — a hash would silently match nothing, the right post-rename behaviour.
        var targetRef = !string.IsNullOrEmpty(input.TargetUserId)
            ? input.TargetUserId
            : input.TargetActorHash;

        if (string.IsNullOrEmpty(input.TenantId) || string.IsNullOrEmpty(targetRef))
            return Ok(new DataHubErasureResult("datahub-reporting", "notApplicable", 0));

        var count = await eraser.AnonymiseSubjectAsync(input.TenantId, targetRef, ct);

        // Never log the raw target reference (PII); the erasure request id is safe.
        logger.LogInformation(
            "DataHub reporting erasure complete. ErasureRequestId={ErasureRequestId} Count={Count}",
            input.ErasureRequestId, count);

        return Ok(new DataHubErasureResult(
            "datahub-reporting", count > 0 ? "anonymised" : "notApplicable", count));
    }
}

public sealed record DataHubErasureInput(string ErasureRequestId, string TenantId, string TargetActorHash, string? TargetUserId = null);
public sealed record DataHubErasureResult(string Service, string Treatment, int AffectedCount, string? Note = null);
