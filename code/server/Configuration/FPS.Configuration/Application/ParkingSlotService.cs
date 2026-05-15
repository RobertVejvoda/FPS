using FPS.Configuration.Domain;

namespace FPS.Configuration.Application;

public sealed class ParkingSlotService(IParkingSlotRepository repository)
{
    public Task<IReadOnlyList<ParkingSlot>> GetByLocationAsync(string tenantId, string locationId, CancellationToken ct)
        => repository.GetByLocationAsync(tenantId, locationId, ct);

    public async Task<IReadOnlyList<string>> ReplaceAsync(string tenantId, string locationId, IReadOnlyList<ParkingSlot> slots, CancellationToken ct)
    {
        var errors = Validate(slots);
        if (errors.Count > 0) return errors;
        await repository.ReplaceLocationSlotsAsync(tenantId, locationId, slots, ct);
        return [];
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<ParkingSlot> slots)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in slots)
        {
            if (string.IsNullOrWhiteSpace(slot.SlotId))
                errors.Add("Each slot must have a non-empty slotId.");
            else if (!seen.Add(slot.SlotId))
                errors.Add($"Duplicate slotId: {slot.SlotId}.");
        }
        return errors;
    }
}
