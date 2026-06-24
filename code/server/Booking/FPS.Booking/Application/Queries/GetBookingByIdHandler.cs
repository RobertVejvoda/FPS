using FPS.Booking.Application.Repositories;
using MediatR;

namespace FPS.Booking.Application.Queries;

public sealed class GetBookingByIdHandler : IRequestHandler<GetBookingByIdQuery, GetBookingByIdResult?>
{
    private readonly IBookingRepository repository;

    public GetBookingByIdHandler(IBookingRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        this.repository = repository;
    }

    public async Task<GetBookingByIdResult?> Handle(GetBookingByIdQuery query, CancellationToken cancellationToken)
    {
        var dto = await repository.GetBookingRequestAsync(query.TenantId, query.RequestId);
        if (dto is null) return null;

        // Employees may only retrieve their own bookings.
        if (!dto.RequestedBy.Equals(query.RequestorId, StringComparison.OrdinalIgnoreCase))
            return null;

        return new GetBookingByIdResult(
            dto.RequestId,
            dto.Status,
            dto.RejectionCode,
            dto.RejectionReason,
            dto.CancellationReason,
            dto.AllocatedSlotId,
            dto.LocationId,
            dto.PlannedArrivalTime,
            dto.PlannedDepartureTime,
            dto.VehicleType,
            dto.VehicleIsElectric,
            dto.RequiresAccessibleSpot,
            dto.VehicleIsCompanyCar,
            dto.RequestedAt,
            dto.LastStatusChangedAt,
            dto.UsageConfirmedAt,
            dto.ConfirmationSource);
    }
}
