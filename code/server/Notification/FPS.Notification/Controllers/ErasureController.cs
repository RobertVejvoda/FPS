using FPS.Notification.Application;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Notification.Controllers;

[ApiController]
public sealed class ErasureController(INotificationRepository repository) : ControllerBase
{
    [HttpPost("/erasure")]
    public async Task<IActionResult> Erase([FromBody] ServiceErasureInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.TargetUserId))
            return Ok(new ServiceErasureResult("notification", "notApplicable", 0));

        var count = await repository.DeleteByRecipientIdAsync(input.TenantId, input.TargetUserId, ct);
        return Ok(new ServiceErasureResult("notification",
            count > 0 ? "deleted" : "notApplicable", count));
    }
}

public sealed record ServiceErasureInput(string ErasureRequestId, string TenantId, string TargetActorHash, string? TargetUserId = null);
public sealed record ServiceErasureResult(string Service, string Treatment, int AffectedCount, string? Note = null);
