namespace FPS.Booking.Domain.ValueObjects;

public class AvailableSlot : ValueObject
{
    public ParkingSlotId SlotId { get; }
    public bool HasCharger { get; }
    public bool IsAccessible { get; }
    public bool IsCompanyCarReserved { get; }

    private AvailableSlot(ParkingSlotId slotId, bool hasCharger, bool isAccessible, bool isCompanyCarReserved)
    {
        SlotId = slotId;
        HasCharger = hasCharger;
        IsAccessible = isAccessible;
        IsCompanyCarReserved = isCompanyCarReserved;
    }

    public static AvailableSlot Create(
        ParkingSlotId slotId,
        bool hasCharger = false,
        bool isAccessible = false,
        bool isCompanyCarReserved = false)
    {
        if (slotId is null)
            throw new BookingException("SlotId is required");

        return new AvailableSlot(slotId, hasCharger, isAccessible, isCompanyCarReserved);
    }

    public bool CanAccommodate(VehicleInformation vehicle)
    {
        if (vehicle.IsCompanyCar && IsCompanyCarReserved) return true;
        if (IsCompanyCarReserved) return false;
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
    }
}
