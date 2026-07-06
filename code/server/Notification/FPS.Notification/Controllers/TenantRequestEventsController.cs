using Dapr;
using FPS.Notification.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Notification.Controllers;

// PLAT004b: pub/sub ingestion for tenant-request alerts. Restricted to Dapr-delivered traffic
// (SEC001) so external callers cannot post fake onboarding alerts.
[ApiController]
[DaprInternalOnly]
public sealed class TenantRequestEventsController(TenantRequestSalesAlertHandler handler) : ControllerBase
{
    private const string PubSubName = "fairspot-pubsub";
    private const string Topic = "tenant-request-received";

    [HttpPost("/notifications/tenant-request-events")]
    [Topic(PubSubName, Topic)]
    public async Task<IActionResult> Handle(TenantRequestEvent @event, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(@event.RequestId))
            return BadRequest();

        await handler.HandleAsync(@event, ct);
        return Ok();
    }
}
