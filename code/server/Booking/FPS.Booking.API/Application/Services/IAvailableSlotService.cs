using FPS.Booking.Domain.ValueObjects;

namespace FPS.Booking.Application.Services;

public interface IAvailableSlotService
{
    Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(
        string tenantId,
        string locationId,
        DateOnly date,
        TimeSlot timeSlot,
        CancellationToken cancellationToken = default);
}
