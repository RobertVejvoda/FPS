namespace FPS.Booking.Domain.ValueObjects;

public class VehicleInformation : ValueObject
{
    public string LicensePlate { get; }
    public VehicleType Type { get; }
    public bool IsElectric { get; }
    public bool RequiresAccessibleSpot { get; }
    public bool IsCompanyCar { get; } // New property

    private VehicleInformation(
        string licensePlate, 
        VehicleType type, 
        bool isElectric, 
        bool requiresAccessibleSpot,
        bool isCompanyCar)
    {
        if (string.IsNullOrWhiteSpace(licensePlate))
            throw new BookingException("License plate is required");
            
        LicensePlate = licensePlate;
        Type = type;
        IsElectric = isElectric;
        RequiresAccessibleSpot = requiresAccessibleSpot;
        IsCompanyCar = isCompanyCar; // Initialize the new property
    }

    public static VehicleInformation Create(
        string licensePlate, 
        VehicleType type, 
        bool isElectric, 
        bool requiresAccessibleSpot,
        bool isCompanyCar) // Updated method signature
    {
        return new VehicleInformation(licensePlate, type, isElectric, requiresAccessibleSpot, isCompanyCar);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return LicensePlate;
        yield return Type;
        yield return IsElectric;
        yield return RequiresAccessibleSpot;
        yield return IsCompanyCar; // Include the new property in equality checks
    }
}