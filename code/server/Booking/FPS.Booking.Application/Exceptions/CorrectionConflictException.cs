namespace FPS.Booking.Application.Exceptions;

public sealed class CorrectionConflictException : Exception
{
    public CorrectionConflictException(Guid requestId, string correctionType, string expectedValue, string actualValue)
        : base($"Correction conflict on {correctionType} for request {requestId}: expected '{expectedValue}' but found '{actualValue}'.") { }
}
