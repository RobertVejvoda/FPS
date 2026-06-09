namespace FPS.Booking.Models;

public record SubmitBookingRequest(
    string FacilityId,
    string? LocationId,
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool RequiresAccessibleSpot,
    bool IsCompanyCar,
    DateTime PlannedArrivalTime,
    DateTime PlannedDepartureTime);
