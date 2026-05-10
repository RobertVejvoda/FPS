namespace FPS.Booking.Application.Models;

public class PenaltyDto
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string RequestorId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string SourceEventId { get; set; } = string.Empty;
}
