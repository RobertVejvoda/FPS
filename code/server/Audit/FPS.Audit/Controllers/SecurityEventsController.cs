using Dapr;
using FPS.Audit.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Audit.Controllers;

// AUTH008B (#734) — security audit evidence (email-verification outcomes) arrives over pub/sub. Like the
// other event subscribers, ingestion is restricted to Dapr-delivered traffic via [DaprInternalOnly] so
// external callers can't post fake security events.
[ApiController]
[DaprInternalOnly]
public sealed class SecurityEventsController(SecurityEventAuditHandler handler) : ControllerBase
{
    private const string PubSubName = "fairspot-pubsub";
    private const string Topic = "security-events";

    [HttpPost("/audit/security-events")]
    [Topic(PubSubName, Topic)]
    public async Task<IActionResult> Handle(SecurityEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(envelope.TenantId) || string.IsNullOrEmpty(envelope.Category) || string.IsNullOrEmpty(envelope.Outcome))
            return BadRequest();

        await handler.HandleAsync(envelope, cancellationToken);
        return Ok();
    }
}
