namespace FPS.Configuration.Domain;

public interface IParkingSlotRepository
{
    Task<IReadOnlyList<ParkingSlot>> GetByLocationAsync(string tenantId, string locationId, CancellationToken cancellationToken = default);
    Task ReplaceLocationSlotsAsync(string tenantId, string locationId, IReadOnlyList<ParkingSlot> slots, CancellationToken cancellationToken = default);
}
