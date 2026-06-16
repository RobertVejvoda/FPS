using Dapr;
using FPS.Reporting.Application;
using FPS.SharedKernel.Filters;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Reporting.Controllers;

// SEC001 (#493): pub/sub ingestion is restricted to Dapr-delivered traffic.
// When APP_API_TOKEN is configured the sidecar attaches dapr-api-token on
// forwarded calls; external callers can't post fake booking events here.
[ApiController]
[DaprInternalOnly]
public sealed class BookingEventsController(BookingEventReportingHandler handler) : ControllerBase
{
    private const string PubSubName = "fps-pubsub";
    private const string Topic = "booking-events";

    [HttpPost("/reporting/booking-events")]
    [Topic(PubSubName, Topic)]
    public async Task<IActionResult> Handle(BookingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(envelope.EventId) || string.IsNullOrEmpty(envelope.TenantId))
            return BadRequest();

        await handler.HandleAsync(envelope, cancellationToken);
        return Ok();
    }
}
