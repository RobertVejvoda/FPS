namespace FPS.Booking.API.Models;

public record BookingResponse(string BookingId, string Status, DateTime SubmissionDate, string Message);