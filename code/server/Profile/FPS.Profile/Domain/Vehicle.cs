namespace FPS.Profile.Domain;

public sealed record Vehicle(
    string VehicleId,
    string LicensePlate,
    string VehicleType,
    bool IsElectric,
    bool IsActive,
    bool IsDefault = false,
    string? Alias = null);
