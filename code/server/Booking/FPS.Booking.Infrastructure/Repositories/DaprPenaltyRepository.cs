using Dapr.Client;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;

namespace FPS.Booking.Infrastructure.Repositories;

public sealed class DaprPenaltyRepository : IPenaltyRepository
{
    private readonly DaprClient daprClient;
    private const string BookingStore = "bookingstore";

    public DaprPenaltyRepository(DaprClient daprClient)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        this.daprClient = daprClient;
    }

    public async Task<bool> ExistsAsync(Guid requestId, string penaltyType, CancellationToken cancellationToken = default)
    {
        var existing = await daprClient.GetStateAsync<PenaltyDto>(
            BookingStore, Key(requestId, penaltyType), cancellationToken: cancellationToken);
        return existing is not null;
    }

    public async Task SaveAsync(PenaltyDto penalty, CancellationToken cancellationToken = default)
        => await daprClient.SaveStateAsync(
            BookingStore, Key(penalty.RequestId, penalty.Type), penalty, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<PenaltyDto>> GetActiveByRequestorAsync(
        string tenantId, string requestorId, DateOnly asOfDate, CancellationToken cancellationToken = default)
    {
        // Full requestor-scoped penalty query requires MongoDB driver.
        // Returns empty until infrastructure test phase implements the read-side.
        await Task.CompletedTask;
        return [];
    }

    private static string Key(Guid requestId, string penaltyType) => $"penalty:{requestId}:{penaltyType}";
}
