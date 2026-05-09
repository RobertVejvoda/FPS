namespace FPS.Booking.Application.Models;

public class BookingRequestDto
{
    public Guid RequestId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid FacilityId { get; set; }
    public DateTime PlannedArrivalTime { get; set; }
    public DateTime PlannedDepartureTime { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending";
}

public class AllocationDto
{
    public Guid AllocationId { get; set; }
    public Guid RequestId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid SlotId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StatusReason { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime AllocatedAt { get; set; }
    public DateTime? ActualArrivalTime { get; set; }
    public string? ArrivalConfirmedBy { get; set; }
    public DateTime? ActualDepartureTime { get; set; }
    public string? DepartureConfirmedBy { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class FacilityAvailabilityResult
{
    public bool IsAvailable { get; set; }
    public int AvailableSlots { get; set; }
    public string? Reason { get; set; }
}

public class SlotAllocationResult
{
    public Guid RequestId { get; set; }
    public Guid SlotId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid VehicleId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string? Reason { get; set; }
}

// External service DTOs
public class FacilityStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class OperatingHoursDto
{
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
}

public class MaintenanceEventDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class VehicleDetailsDto
{
    public string VehicleType { get; set; } = string.Empty;
}

public class ParkingSlotDto
{
    public Guid SlotId { get; set; }
    public string SlotNumber { get; set; } = string.Empty;
    public string SlotType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class UpdateAllocationInfo
{
    public Guid AllocationId { get; set; }
    public DateTime ArrivalTime { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
}

public class CancellationInfo
{
    public Guid AllocationId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CancelledBy { get; set; } = string.Empty;
    public DateTime CancellationTime { get; set; }
}

public class AllocatedSlotDto
{
    public Guid AllocationId { get; set; }
    public Guid SlotId { get; set; }
    public Guid FacilityId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class ArrivalConfirmationDto
{
    public DateTime ArrivalTime { get; set; }
    public string ConfirmedBy { get; set; } = string.Empty;
}

public class ReservationCancellationDto
{
    public string CancellationReason { get; set; } = string.Empty;
    public string CancelledBy { get; set; } = string.Empty;
    public DateTime CancellationTime { get; set; }
}
