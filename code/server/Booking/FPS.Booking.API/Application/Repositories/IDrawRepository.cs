using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Repositories;

public interface IDrawRepository
{
    Task<DrawAttemptDto?> GetByKeyAsync(string drawKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the Draw attempt with optimistic concurrency control where supported.
    /// If the attempt has an ETag, the save will fail if another update modified the state.
    /// For Dapr state stores without ETag support, updates use last-write-wins.
    /// </summary>
    Task SaveAsync(DrawAttemptDto attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to save the Draw attempt with concurrency check. Returns true if saved successfully,
    /// false if there was a concurrency conflict (ETag mismatch).
    /// </summary>
    Task<bool> TrySaveAsync(DrawAttemptDto attempt, CancellationToken cancellationToken = default);
}
