namespace FPS.Booking.Domain.ValueObjects;

public sealed class AvailableSlot : ValueObject
{
    public ParkingSlotId SlotId { get; }
    public bool HasCharger { get; }
    public bool IsAccessible { get; }
    public bool IsCompanyCarReserved { get; }

    // Motorcycle-specific slot: a physical motorcycle area that holds multiple bikes.
    // For Draw allocation purposes a multi-unit area is expanded into N individual
    // AvailableSlot instances (one per unit) by the capacity loader, so the Draw
    // algorithm still treats each AvailableSlot as one allocatable unit.
    // When this flag is true, only motorcycles may use the slot (in v1).
    public bool IsMotorcycleCapacity { get; }

    private AvailableSlot(
        ParkingSlotId slotId,
        bool hasCharger,
        bool isAccessible,
        bool isCompanyCarReserved,
        bool isMotorcycleCapacity)
    {
        SlotId = slotId;
        HasCharger = hasCharger;
        IsAccessible = isAccessible;
        IsCompanyCarReserved = isCompanyCarReserved;
        IsMotorcycleCapacity = isMotorcycleCapacity;
    }

    public static AvailableSlot Create(
        ParkingSlotId slotId,
        bool hasCharger = false,
        bool isAccessible = false,
        bool isCompanyCarReserved = false,
        bool isMotorcycleCapacity = false)
    {
        ArgumentNullException.ThrowIfNull(slotId);
        return new AvailableSlot(slotId, hasCharger, isAccessible, isCompanyCarReserved, isMotorcycleCapacity);
    }

    public bool CanAccommodate(VehicleInformation vehicle)
    {
        // Company-car-reserved slots are car-only — and the company-car vehicle must own them.
        if (IsCompanyCarReserved && vehicle.IsCompanyCar) return true;
        if (IsCompanyCarReserved) return false;

        // Motorcycle-specific capacity is motorcycle-only in v1. A car/SUV/van must not consume it.
        if (IsMotorcycleCapacity && vehicle.Type != VehicleType.Motorcycle) return false;

        if (vehicle.IsElectric && !HasCharger) return false;
        if (vehicle.RequiresAccessibleSpot && !IsAccessible) return false;
        return true;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SlotId;
        yield return HasCharger;
        yield return IsAccessible;
        yield return IsCompanyCarReserved;
        yield return IsMotorcycleCapacity;
    }
}
