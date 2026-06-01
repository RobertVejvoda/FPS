using Dapr;
using FPS.DataHub.Application;
using Microsoft.AspNetCore.Mvc;

namespace FPS.DataHub.Controllers;

[ApiController]
public sealed class BookingEventsController(EventInboxService inbox) : ControllerBase
{
    private const string PubSubName = "fps-pubsub";
    private const string Topic = "booking-events";

    [HttpPost("/datahub/booking-events")]
    [Topic(PubSubName, Topic)]
    public async Task<IActionResult> Handle(BookingEventEnvelope envelope, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(envelope.EventId) || string.IsNullOrEmpty(envelope.TenantId))
            return BadRequest();

        await inbox.AcceptAsync(envelope, ct);
        return Ok();
    }
}
