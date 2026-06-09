using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Infrastructure.Repositories;

public sealed class DaprDrawRepository : IDrawRepository
{
    private readonly DaprClient daprClient;
    private const string BookingStore = "bookingstore";

    public DaprDrawRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<DrawAttemptDto?> GetByKeyAsync(string drawKey, CancellationToken cancellationToken = default)
    {
        var (value, etag) = await daprClient.GetStateAndETagAsync<DrawAttemptDto>(
            BookingStore, drawKey, cancellationToken: cancellationToken);

        if (value != null)
        {
            value.ETag = etag;
        }

        return value;
    }

    public async Task SaveAsync(DrawAttemptDto attempt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attempt.ETag))
        {
            // No ETag — use simple save (last-write-wins)
            await daprClient.SaveStateAsync(BookingStore, attempt.DrawKey, attempt, cancellationToken: cancellationToken);
        }
        else
        {
            // ETag present — use optimistic concurrency with TrySaveStateAsync
            var success = await daprClient.TrySaveStateAsync(
                BookingStore, attempt.DrawKey, attempt, attempt.ETag, cancellationToken: cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException(
                    $"Failed to save Draw attempt '{attempt.DrawKey}': ETag mismatch indicates concurrent modification. " +
                    "The Draw attempt was modified by another process.");
            }
        }
    }

    public async Task<bool> TrySaveAsync(DrawAttemptDto attempt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attempt.ETag))
        {
            // No ETag — use simple save and return true
            await daprClient.SaveStateAsync(BookingStore, attempt.DrawKey, attempt, cancellationToken: cancellationToken);
            return true;
        }

        // ETag present — use optimistic concurrency
        return await daprClient.TrySaveStateAsync(
            BookingStore, attempt.DrawKey, attempt, attempt.ETag, cancellationToken: cancellationToken);
    }
}
