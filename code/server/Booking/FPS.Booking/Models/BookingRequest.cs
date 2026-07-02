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
    DateTime PlannedDepartureTime,
    // PLAT-seats (#710) — "Parking" (default) or "Seats". Seat requests carry no vehicle fields;
    // the vehicle fields above are ignored for seats.
    string? ResourceType = null);
