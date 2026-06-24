using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed record GetBookingByIdQuery(
    string TenantId,
    string RequestorId,
    Guid RequestId) : IRequest<GetBookingByIdResult?>;

public sealed record GetBookingByIdResult(
    Guid RequestId,
    string Status,
    string? RejectionCode,
    string? RejectionReason,
    string? CancellationReason,
    string? AllocatedSlotId,
    string? LocationId,
    DateTime PlannedArrivalTime,
    DateTime PlannedDepartureTime,
    string? VehicleType,
    bool VehicleIsElectric,
    bool RequiresAccessibleSpot,
    bool VehicleIsCompanyCar,
    DateTime RequestedAt,
    DateTime LastStatusChangedAt,
    DateTime? UsageConfirmedAt,
    string? ConfirmationSource);
