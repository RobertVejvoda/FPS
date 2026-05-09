using System;

namespace FPS.Booking.Domain.Models
{
    public class BookingRequestDto
    {
        public Guid RequestId { get; set; }
        public Guid VehicleId { get; set; }
        public Guid FacilityId { get; set; }
        public DateTime PlannedArrivalTime { get; set; }
        public DateTime PlannedDepartureTime { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Pending";
    }

    public class AllocationDto
    {
        public Guid AllocationId { get; set; }
        public Guid RequestId { get; set; }
        public Guid FacilityId { get; set; }
        public Guid SlotId { get; set; }
        public string Status { get; set; }
        public string StatusReason { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime AllocatedAt { get; set; }
        public DateTime? ActualArrivalTime { get; set; }
        public string ArrivalConfirmedBy { get; set; }
        public DateTime? ActualDepartureTime { get; set; }
        public string DepartureConfirmedBy { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class AllocatedSlotDto
    {
        public Guid AllocationId { get; set; }
        public Guid SlotId { get; set; }
        public Guid FacilityId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class FacilityAvailabilityResult
    {
        public bool IsAvailable { get; set; }
        public string Reason { get; set; }
    }

    public class SlotAllocationResult
    {
        public bool Success { get; set; }
        public Guid RequestId { get; set; }
        public Guid SlotId { get; set; }
        public Guid FacilityId { get; set; }
        public Guid VehicleId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Reason { get; set; }
    }

    public class UpdateAllocationInfo
    {
        public Guid AllocationId { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string ConfirmedBy { get; set; }
    }

    public class CancellationInfo
    {
        public Guid AllocationId { get; set; }
        public string Reason { get; set; }
        public string CancelledBy { get; set; }
        public DateTime CancellationTime { get; set; }
    }

    public class ArrivalConfirmationDto
    {
        public DateTime ArrivalTime { get; set; }
        public string ConfirmedBy { get; set; }
    }

    public class ReservationCancellationDto
    {
        public string CancellationReason { get; set; }
        public string CancelledBy { get; set; }
        public DateTime CancellationTime { get; set; }
    }

    public class AllocationStatusDto
    {
        public Guid RequestId { get; set; }
        public string Status { get; set; }
        public string CurrentPhase { get; set; }
        public DateTime? LastUpdated { get; set; }
        public AllocatedSlotDto AllocatedSlot { get; set; }
    }

    public class AvailabilityResultDto
    {
        public bool HasAvailability { get; set; }
        public int AvailableSlots { get; set; }
        public string[] AvailableSlotTypes { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    // External service DTOs
    public class FacilityStatusDto
    {
        public string Status { get; set; }
        public string Reason { get; set; }
    }

    public class OperatingHoursDto
    {
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
    }

    public class MaintenanceEventDto
    {
        public Guid EventId { get; set; }
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public class VehicleDetailsDto
    {
        public Guid VehicleId { get; set; }
        public string VehicleType { get; set; }
        public string RegistrationNumber { get; set; }
    }

    public class ParkingSlotDto
    {
        public Guid SlotId { get; set; }
        public string SlotNumber { get; set; }
        public string SlotType { get; set; }
        public string Status { get; set; }
    }
}