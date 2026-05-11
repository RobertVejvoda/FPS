using Dapr;
using FPS.Notification.Application;
using Microsoft.AspNetCore.Mvc;

namespace FPS.Notification.Controllers;

[ApiController]
public sealed class BookingEventsController(BookingEventNotificationHandler handler) : ControllerBase
{
    private const string PubSubName = "fps-pubsub";
    private const string Topic = "booking-events";

    [HttpPost("/notifications/booking-events")]
    [Topic(PubSubName, Topic)]
    public async Task<IActionResult> Handle(BookingEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(envelope.EventId) || string.IsNullOrEmpty(envelope.TenantId))
            return BadRequest();

        await handler.HandleAsync(envelope, cancellationToken);
        return Ok();
    }
}
