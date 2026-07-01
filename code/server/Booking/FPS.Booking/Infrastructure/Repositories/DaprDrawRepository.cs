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

        // Record the draw key in the per-tenant index so a destructive tenant purge (PLAT003C)
        // reaches every attempt — including empty/failed runs with no booking-request row.
        await AppendToDrawIndexAsync(attempt.TenantId, attempt.DrawKey, cancellationToken);
    }

    public async Task<bool> TrySaveAsync(DrawAttemptDto attempt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attempt.ETag))
        {
            // No ETag — use simple save and return true
            await daprClient.SaveStateAsync(BookingStore, attempt.DrawKey, attempt, cancellationToken: cancellationToken);
            await AppendToDrawIndexAsync(attempt.TenantId, attempt.DrawKey, cancellationToken);
            return true;
        }

        // ETag present — use optimistic concurrency
        var success = await daprClient.TrySaveStateAsync(
            BookingStore, attempt.DrawKey, attempt, attempt.ETag, cancellationToken: cancellationToken);

        // Only index the key once the state actually persisted.
        if (success)
            await AppendToDrawIndexAsync(attempt.TenantId, attempt.DrawKey, cancellationToken);

        return success;
    }

    public async Task<int> PurgeTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        // The per-tenant draw index is the master list of every attempt store key for the tenant,
        // so enumerating it reaches empty/failed/archived runs that have no booking-request row.
        var indexKey = DrawIndexKey(tenantId);
        var drawKeys = await daprClient.GetStateAsync<List<string>>(
            BookingStore, indexKey, cancellationToken: cancellationToken) ?? [];

        var removed = 0;
        foreach (var drawKey in drawKeys)
        {
            await daprClient.DeleteStateAsync(BookingStore, drawKey, cancellationToken: cancellationToken);
            removed++;
        }

        // Remove the index last so a re-run finds nothing, enumerates nothing, and returns 0.
        await daprClient.DeleteStateAsync(BookingStore, indexKey, cancellationToken: cancellationToken);
        return removed;
    }

    private static string DrawIndexKey(string tenantId)
        => TenantStorageKey.For("draw-index", tenantId, "all");

    private async Task AppendToDrawIndexAsync(string tenantId, string drawKey, CancellationToken cancellationToken)
    {
        // Index bookkeeping must never break the core save. Every production save path sets a
        // tenant id (Acquire/Fail/CompleteDrawAttempt + TriggerDrawHandler), so a blank one only
        // occurs for un-tenanted test fixtures — skip indexing it rather than throwing on Sanitise.
        if (string.IsNullOrWhiteSpace(tenantId))
            return;

        var indexKey = DrawIndexKey(tenantId);
        var index = await daprClient.GetStateAsync<List<string>>(
            BookingStore, indexKey, cancellationToken: cancellationToken) ?? [];

        if (index.Contains(drawKey))
            return;

        index.Add(drawKey);
        await daprClient.SaveStateAsync(BookingStore, indexKey, index, cancellationToken: cancellationToken);
    }
}
