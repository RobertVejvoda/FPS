using FPS.Configuration.Domain;

namespace FPS.Configuration.Application;

// Green Logistics demo parking facility layout, used by the Development startup seed
// (see Program.cs). Kept in its own type so the slot mix is unit-testable without
// standing up the host.
public static class DemoConfigurationSeed
{
    // 20 active slots with human-readable labels — the SlotId is what the parking map
    // and HR views render (there is no separate label field). The feature mix lets the
    // seeded population exercise every allocation path: a fair Tier-2 draw on the general
    // slots, EV-charger and accessible preferences, company-car Tier-1 fixed slots, and a
    // shared motorcycle area.
    //   A-01..A-13     general          (13)
    //   EV-01..EV-03   charger          (3)
    //   ACC-01         accessible       (1)
    //   VIP-01..VIP-02 company-car only (2)
    //   MOTO-01        motorcycle area  (1, holds 4)
    public static IReadOnlyList<ParkingSlot> BuildSlots(string tenantId, string locationId)
    {
        ParkingSlot Slot(string slotId, bool charger = false, bool accessible = false,
            bool companyCarOnly = false, bool motorcycle = false, int? motorcycleUnits = null) => new()
        {
            SlotId = slotId,
            TenantId = tenantId,
            LocationId = locationId,
            IsActive = true,
            HasCharger = charger,
            IsAccessible = accessible,
            IsCompanyCarOnly = companyCarOnly,
            IsMotorcycleCapacity = motorcycle,
            MotorcycleCapacityUnits = motorcycleUnits,
        };

        var slots = new List<ParkingSlot>();
        for (var i = 1; i <= 13; i++)
            slots.Add(Slot($"A-{i:D2}"));
        slots.Add(Slot("EV-01", charger: true));
        slots.Add(Slot("EV-02", charger: true));
        slots.Add(Slot("EV-03", charger: true));
        slots.Add(Slot("ACC-01", accessible: true));
        slots.Add(Slot("VIP-01", companyCarOnly: true));
        slots.Add(Slot("VIP-02", companyCarOnly: true));
        slots.Add(Slot("MOTO-01", motorcycle: true, motorcycleUnits: 4));
        return slots;
    }
}
