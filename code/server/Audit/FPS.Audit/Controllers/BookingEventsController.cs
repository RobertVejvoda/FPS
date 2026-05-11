using Dapr;
using FPS.Audit.Application;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Audit.Controllers;

[ApiController]
public sealed class BookingEventsController(BookingEventAuditHandler handler) : ControllerBase
{
    private const string PubSubName = "fps-pubsub";
    private const string Topic = "booking-events";

    [HttpPost("/audit/booking-events")]
    [Topic(PubSubName, Topic)]
    public async Task<IActionResult> Handle(BookingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(envelope.EventId) || string.IsNullOrEmpty(envelope.TenantId))
            return BadRequest();

        await handler.HandleAsync(envelope, cancellationToken);
        return Ok();
    }
}
