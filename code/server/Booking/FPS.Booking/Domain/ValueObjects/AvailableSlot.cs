namespace FPS.Booking.Domain.ValueObjects;

public sealed class AvailableSlot : ValueObject
{
    public ParkingSlotId SlotId { get; }
    public bool IsActive { get; }
    public bool HasCharger { get; }
    public bool IsAccessible { get; }
    public bool IsCompanyCarReserved { get; }
    public string? ReservedForUserId { get; }

    // Motorcycle-specific slot: a physical motorcycle area that holds multiple bikes.
    // For Draw allocation purposes a multi-unit area is expanded into N individual
    // AvailableSlot instances (one per unit) by the capacity loader, so the Draw
    // algorithm still treats each AvailableSlot as one allocatable unit.
    // When this flag is true, only motorcycles may use the slot (in v1).
    public bool IsMotorcycleCapacity { get; }

    private AvailableSlot(
        ParkingSlotId slotId,
        bool isActive,
        bool hasCharger,
        bool isAccessible,
        bool isCompanyCarReserved,
        string? reservedForUserId,
        bool isMotorcycleCapacity)
    {
        SlotId = slotId;
        IsActive = isActive;
        HasCharger = hasCharger;
        IsAccessible = isAccessible;
        IsCompanyCarReserved = isCompanyCarReserved;
        ReservedForUserId = NormalizeReservedForUserId(reservedForUserId);
        IsMotorcycleCapacity = isMotorcycleCapacity;
    }

    public static AvailableSlot Create(
        ParkingSlotId slotId,
        bool isActive = true,
        bool hasCharger = false,
        bool isAccessible = false,
        bool isCompanyCarReserved = false,
        string? reservedForUserId = null,
        bool isMotorcycleCapacity = false)
    {
        ArgumentNullException.ThrowIfNull(slotId);
        return new AvailableSlot(
            slotId,
            isActive,
            hasCharger,
            isAccessible,
            isCompanyCarReserved,
            reservedForUserId,
            isMotorcycleCapacity);
    }

    public bool CanAccommodate(VehicleInformation vehicle)
    {
        if (!IsActive) return false;

        // Company-car-reserved slots are car-only.
        if (IsCompanyCarReserved && !vehicle.IsCompanyCar) return false;

        // Motorcycle-specific capacity is motorcycle-only in v1. A car/SUV/van must not consume it.
        if (IsMotorcycleCapacity && vehicle.Type != VehicleType.Motorcycle) return false;

        if (vehicle.IsElectric && !HasCharger) return false;
        if (vehicle.RequiresAccessibleSpot && !IsAccessible) return false;
        return true;
    }

    public bool IsReservedFor(UserId requestorId)
    {
        ArgumentNullException.ThrowIfNull(requestorId);
        return !string.IsNullOrWhiteSpace(ReservedForUserId)
            && string.Equals(ReservedForUserId, requestorId.Value.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeReservedForUserId(string? reservedForUserId)
        => string.IsNullOrWhiteSpace(reservedForUserId) ? null : reservedForUserId.Trim();

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SlotId;
        yield return IsActive;
        yield return HasCharger;
        yield return IsAccessible;
        yield return IsCompanyCarReserved;
        yield return ReservedForUserId ?? string.Empty;
        yield return IsMotorcycleCapacity;
    }
}
