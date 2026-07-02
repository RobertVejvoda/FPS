using FPS.Booking.Application.Models;
using FPS.Booking.Domain;
using MediatR;

namespace FPS.Booking.Application.Commands;

public record SubmitBookingRequestCommand(
    string TenantId,
    string RequestorId,
    string FacilityId,
    string? LocationId,
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool RequiresAccessibleSpot,
    bool IsCompanyCar,
    DateTime PlannedArrivalTime,
    DateTime PlannedDepartureTime,
    // PLAT-seats (#710) — defaults to Parking so existing callers are unchanged.
    ResourceType ResourceType = ResourceType.Parking) : IRequest<SubmitBookingRequestResult>;
