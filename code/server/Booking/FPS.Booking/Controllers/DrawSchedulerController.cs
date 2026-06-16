using FPS.Booking.Application.Services;
using FPS.SharedKernel.Filters;
using FPS.SharedKernel.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.Controllers;

// Receives Dapr cron binding ticks. The route must match the Dapr component name.
// Multiple Booking replicas may receive the same tick; idempotency is guaranteed by the
// deterministic workflow instance ID in DaprDrawWorkflowStarter and TriggerDrawHandler's
// existing-draw checks.
//
// SEC002 (#494): the endpoint is now gated by [DaprInternalOnly]. The Dapr
// sidecar injects dapr-api-token on every call it forwards; external
// callers can't reach this anonymously in any environment where
// APP_API_TOKEN is configured. Outside Development, the guard also fails
// closed when the token is missing, so a forgotten config does not silently
// open the scheduler.
[ApiController]
[Route("draw-scheduler")]
[AllowAnonymous]
[DaprInternalOnly]
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

        await schedulerService.TriggerDueDrawsAsync(targetDate, cancellationToken: cancellationToken);

        // Dapr requires 2xx to acknowledge receipt; 200 is idiomatic for bindings.
        return Ok();
    }
}
