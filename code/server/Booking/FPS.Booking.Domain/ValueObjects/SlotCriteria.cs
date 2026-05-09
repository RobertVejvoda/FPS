namespace FPS.Booking.Domain.ValueObjects;

public class SlotCriteria : ValueObject
{
    public bool NeedsAccessibility { get; }
    public bool NeedsCharger { get; }
    public bool IsForCompanyCar { get; }
    public string PreferredLocation { get; }
    public int Priority { get; } // Higher number means higher priority
    
    private SlotCriteria(
        bool needsAccessibility,
        bool needsCharger,
        bool isForCompanyCar,
        string preferredLocation,
        int priority)
    {
        NeedsAccessibility = needsAccessibility;
        NeedsCharger = needsCharger;
        IsForCompanyCar = isForCompanyCar;
        PreferredLocation = preferredLocation;
        Priority = priority;
    }
    
    public static SlotCriteria FromVehicle(VehicleInformation vehicle, string preferredLocation = "", int priority = 0)
    {
        return new SlotCriteria(
            vehicle.RequiresAccessibleSpot,
            vehicle.IsElectric,
            vehicle.IsCompanyCar,
            preferredLocation,
            priority
        );
    }
    
    public static SlotCriteria Create(
        bool needsAccessibility = false,
        bool needsCharger = false,
        bool isForCompanyCar = false,
        string preferredLocation = "",
        int priority = 0)
    {
        return new SlotCriteria(
            needsAccessibility,
            needsCharger,
            isForCompanyCar,
            preferredLocation,
            priority
        );
    }
    
    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return NeedsAccessibility;
        yield return NeedsCharger;
        yield return IsForCompanyCar;
        yield return PreferredLocation;
        yield return Priority;
    }
}