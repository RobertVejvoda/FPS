using Microsoft.AspNetCore.Mvc;

namespace FPS.Booking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    [HttpPost]
    public IActionResult SubmitBookingRequest() => StatusCode(501, "Not implemented — Phase 1");

    [HttpGet("{instanceId}/status")]
    public IActionResult GetBookingStatus(string instanceId) => StatusCode(501, "Not implemented — Phase 1");

    [HttpPost("{instanceId}/arrival")]
    public IActionResult ConfirmArrival(string instanceId) => StatusCode(501, "Not implemented — Phase 1");

    [HttpPost("{instanceId}/cancel")]
    public IActionResult CancelReservation(string instanceId) => StatusCode(501, "Not implemented — Phase 1");

    [HttpGet("availability")]
    public IActionResult CheckAvailability() => StatusCode(501, "Not implemented — Phase 1");
}
