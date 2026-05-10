namespace FPS.Booking.Application.Models;

public class BookingRequestDto
{
    public Guid RequestId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid FacilityId { get; set; }
    public string? LocationId { get; set; }
    public DateTime PlannedArrivalTime { get; set; }
    public DateTime PlannedDepartureTime { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? AllocatedSlotId { get; set; }
    public DateTime LastStatusChangedAt { get; set; }
    public DateTime? UsageConfirmedAt { get; set; }
    public string? ConfirmationSource { get; set; }
    public string? ConfirmationSourceEventId { get; set; }
}
