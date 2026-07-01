using FPS.Customer.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Customer.Controllers;

// PLAT003B — receives Dapr cron binding ticks for the nightly sandbox reset. The route must match the
// Dapr component name ("sandbox-reset-scheduler"). Multiple Customer replicas may receive the same tick;
// the per-window lease in ScheduledSandboxResetService plus the idempotent reset ensure at most one
// effective reset per window (mirrors DrawSchedulerController).
//
// [DaprInternalOnly]: the Dapr sidecar injects dapr-api-token on every call it forwards, so external
// callers cannot reach this anonymously where APP_API_TOKEN is configured, and the guard fails closed
// outside Development if the token is missing. Excluded from the open OpenAPI client.
[ApiController]
[Route("sandbox-reset-scheduler")]
[AllowAnonymous]
[DaprInternalOnly]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class SandboxResetSchedulerController(ScheduledSandboxResetService scheduler) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> OnSchedulerTick(CancellationToken ct)
    {
        await scheduler.RunDueResetsAsync(ct);
        // Dapr requires 2xx to acknowledge the binding tick; 200 is idiomatic for bindings.
        return Ok();
    }
}
