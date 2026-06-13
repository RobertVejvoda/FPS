using FPS.Configuration.Domain;

namespace FPS.Configuration.Application;

public sealed class ParkingSlotService(IParkingSlotRepository repository, ISlotChangeRepository changeRepository)
{
    public Task<IReadOnlyList<ParkingSlot>> GetByLocationAsync(string tenantId, string locationId, CancellationToken ct)
        => repository.GetByLocationAsync(tenantId, locationId, ct);

    public Task<IReadOnlyList<SlotChangeRecord>> GetChangeHistoryAsync(string tenantId, string locationId, int limit, CancellationToken ct)
        => changeRepository.GetHistoryAsync(tenantId, locationId, limit, ct);

    public async Task<IReadOnlyList<string>> ReplaceAsync(
        string tenantId, string locationId, IReadOnlyList<ParkingSlot> slots,
        string changedByUserId, string? changeReason, CancellationToken ct)
    {
        var errors = Validate(slots);
        if (errors.Count > 0) return errors;
        await repository.ReplaceLocationSlotsAsync(tenantId, locationId, slots, ct);
        await changeRepository.RecordAsync(new SlotChangeRecord
        {
            TenantId = tenantId,
            LocationId = locationId,
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangeReason = string.IsNullOrWhiteSpace(changeReason) ? null : changeReason.Trim(),
            SlotCount = slots.Count,
        }, ct);
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

            // Reject motorcycleCapacityUnits on non-motorcycle slots so HR doesn't
            // accidentally save a unit count that has no effect, and bound the value
            // so a typo cannot spawn a thousand allocation units on Draw.
            if (slot.MotorcycleCapacityUnits is { } units)
            {
                if (!slot.IsMotorcycleCapacity)
                    errors.Add($"Slot {slot.SlotId}: motorcycleCapacityUnits requires isMotorcycleCapacity=true.");
                else if (units <= 0 || units > 20)
                    errors.Add($"Slot {slot.SlotId}: motorcycleCapacityUnits must be between 1 and 20.");
            }
        }
        return errors;
    }
}
