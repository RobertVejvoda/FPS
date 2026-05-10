using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Repositories;

public interface IDrawRepository
{
    Task<DrawAttemptDto?> GetByKeyAsync(string drawKey, CancellationToken cancellationToken = default);
    Task SaveAsync(DrawAttemptDto attempt, CancellationToken cancellationToken = default);
}
