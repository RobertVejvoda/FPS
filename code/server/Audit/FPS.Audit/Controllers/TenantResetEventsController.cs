using Dapr;
using FPS.Audit.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Audit.Controllers;

// PLAT003C-C2: sandbox-reset audit evidence arrives over pub/sub. Like the
// booking-events subscriber, ingestion is restricted to Dapr-delivered traffic
// via [DaprInternalOnly] so external callers can't post fake reset events.
[ApiController]
[DaprInternalOnly]
public sealed class TenantResetEventsController(SandboxResetAuditHandler handler) : ControllerBase
{
    private const string PubSubName = "fairspot-pubsub";
    private const string Topic = "tenant-reset-events";

    [HttpPost("/audit/tenant-reset-events")]
    [Topic(PubSubName, Topic)]
    public async Task<IActionResult> Handle(TenantResetEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(envelope.TenantId) || string.IsNullOrEmpty(envelope.Action))
            return BadRequest();

        await handler.HandleAsync(envelope, cancellationToken);
        return Ok();
    }
}
