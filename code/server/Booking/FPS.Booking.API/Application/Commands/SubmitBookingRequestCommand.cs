using FPS.Booking.Application.Models;
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
    DateTime PlannedDepartureTime) : IRequest<SubmitBookingRequestResult>;
