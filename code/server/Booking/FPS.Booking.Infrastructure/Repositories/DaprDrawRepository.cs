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
        => await daprClient.GetStateAsync<DrawAttemptDto>(BookingStore, drawKey, cancellationToken: cancellationToken);

    public async Task SaveAsync(DrawAttemptDto attempt, CancellationToken cancellationToken = default)
        => await daprClient.SaveStateAsync(BookingStore, attempt.DrawKey, attempt, cancellationToken: cancellationToken);
}
