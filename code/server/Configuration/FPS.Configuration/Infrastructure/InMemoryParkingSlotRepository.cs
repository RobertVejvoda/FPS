using FPS.Configuration.Domain;

namespace FPS.Configuration.Infrastructure;

public sealed class InMemoryParkingSlotRepository : IParkingSlotRepository
{
    private readonly Dictionary<(string tenantId, string locationId), List<ParkingSlot>> store = new();
    private readonly Lock gate = new();

    public Task<IReadOnlyList<ParkingSlot>> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            store.TryGetValue((tenantId, locationId), out var slots);
            return Task.FromResult<IReadOnlyList<ParkingSlot>>(slots?.ToList() ?? []);
        }
    }

    public Task ReplaceLocationSlotsAsync(string tenantId, string locationId, IReadOnlyList<ParkingSlot> slots, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            store[(tenantId, locationId)] = [.. slots];
        }
        return Task.CompletedTask;
    }
}
