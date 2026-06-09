namespace FPS.Booking.Application.Exceptions;

public sealed class BookingNotFoundException : Exception
{
    public BookingNotFoundException(Guid requestId)
        : base($"Booking request {requestId} was not found.") { }
}
