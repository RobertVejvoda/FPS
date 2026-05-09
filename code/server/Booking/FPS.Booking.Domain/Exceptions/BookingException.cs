using FPS.SharedKernel.Exceptions;

namespace FPS.Booking.Domain.Exceptions;

public class BookingException : BaseException
{
    public BookingException(string message) : base(message) { }

    public BookingException(string message, Exception innerException) : base(message, innerException) { }
}