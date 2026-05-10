using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Repositories;

public interface IPenaltyRepository
{
    Task<bool> ExistsAsync(Guid requestId, string penaltyType, CancellationToken cancellationToken = default);
    Task SaveAsync(PenaltyDto penalty, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PenaltyDto>> GetActiveByRequestorAsync(string tenantId, string requestorId, DateOnly asOfDate, CancellationToken cancellationToken = default);
}
