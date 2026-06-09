namespace FPS.Booking.Application.Models
{
    public record BookingResponse(string BookingId, string Status, DateTime SubmissionDate, string Message);
}