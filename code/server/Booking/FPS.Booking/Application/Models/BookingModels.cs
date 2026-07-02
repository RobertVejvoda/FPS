namespace FPS.Booking.Application.Models;

public class BookingRequestDto
{
    public Guid RequestId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public Guid FacilityId { get; set; }
    public string? LocationId { get; set; }
    // PLAT-seats (#710) — "Parking" (default) or "Seats". Requests persisted before this field
    // deserialise with null and are treated as Parking everywhere they are read.
    public string? ResourceType { get; set; }
    public DateTime PlannedArrivalTime { get; set; }
    public DateTime PlannedDepartureTime { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
    public string? RejectionCode { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    // Allocated slot reference — stored as a string so multi-unit motorcycle
    // ids like "M1-1" / "M1-2" round-trip intact. Older Dapr state with GUID
    // slot ids deserialises as a string transparently.
    public string? AllocatedSlotId { get; set; }
    public DateTime LastStatusChangedAt { get; set; }
    public DateTime? UsageConfirmedAt { get; set; }
    public string? ConfirmationSource { get; set; }
    public string? ConfirmationSourceEventId { get; set; }
    public string? ProfileSnapshotVersion { get; set; }
    // Vehicle facts captured at submission time. RunAllocationActivity restores
    // BookingRequest with these so the Draw can match motorcycle capacity and
    // charger/accessibility correctly instead of defaulting every pending
    // request to Sedan.
    public string? VehicleType { get; set; }
    public bool VehicleIsElectric { get; set; }
    public bool RequiresAccessibleSpot { get; set; }
    public bool VehicleIsCompanyCar { get; set; }
}
