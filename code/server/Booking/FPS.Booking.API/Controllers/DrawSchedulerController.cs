using FPS.Booking.Application.Services;
using FPS.SharedKernel.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

// Receives Dapr cron binding ticks. The route must match the Dapr component name.
// Multiple Booking replicas may receive the same tick; idempotency is guaranteed by the
// deterministic workflow instance ID in DaprDrawWorkflowStarter and TriggerDrawHandler's
// existing-draw checks. This endpoint intentionally has no JWT auth — it is only reachable
// via the Dapr sidecar on localhost and must be protected at network level in production.
[ApiController]
[Route("draw-scheduler")]
[AllowAnonymous]
public sealed class DrawSchedulerController(
    IDrawSchedulerService schedulerService,
    DrawSchedulerOptions schedulerOptions,
    ISystemClock clock) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> OnSchedulerTick(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var targetDate = today.AddDays(schedulerOptions.TargetDateOffsetDays);

        await schedulerService.TriggerDueDrawsAsync(targetDate, cancellationToken);

        // Dapr requires 2xx to acknowledge receipt; 200 is idiomatic for bindings.
        return Ok();
    }
}
