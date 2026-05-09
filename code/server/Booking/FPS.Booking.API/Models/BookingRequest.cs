namespace FPS.Booking.API.Models;

public record BookingRequest(
    Guid VehicleId,
    Guid FacilityId,
    DateTime PlannedArrivalTime,
    DateTime PlannedDepartureTime);
