namespace FPS.Booking.Tests.Domain.ValueObjects;

public sealed class AvailableSlotTests
{
    [Fact]
    public void CanAccommodate_Motorcycle_OnNormalSlot_IsTrue()
    {
        // The v1 product rule (issue #468): a motorcycle CAN use an ordinary slot.
        // It consumes the entire slot as one vehicle.
        var slot = AvailableSlot.Create(ParkingSlotId.FromString("101"));
        var motorcycle = Motorcycle();
        Assert.True(slot.CanAccommodate(motorcycle));
    }

    [Fact]
    public void CanAccommodate_Motorcycle_OnMotorcycleSlot_IsTrue()
    {
        var slot = AvailableSlot.Create(ParkingSlotId.FromString("M1"), isMotorcycleCapacity: true);
        Assert.True(slot.CanAccommodate(Motorcycle()));
    }

    [Fact]
    public void CanAccommodate_Sedan_OnMotorcycleSlot_IsFalse()
    {
        // Motorcycle-specific capacity is motorcycle-only in v1 — cars/SUVs/vans must not consume it.
        var slot = AvailableSlot.Create(ParkingSlotId.FromString("M1"), isMotorcycleCapacity: true);
        Assert.False(slot.CanAccommodate(Sedan()));
    }

    [Fact]
    public void CanAccommodate_Suv_OnMotorcycleSlot_IsFalse()
    {
        var slot = AvailableSlot.Create(ParkingSlotId.FromString("M1"), isMotorcycleCapacity: true);
        Assert.False(slot.CanAccommodate(VehicleInformation.Create("SUV-001", VehicleType.SUV, false, false, false)));
    }

    [Fact]
    public void CanAccommodate_CompanyCar_OnMotorcycleSlot_IsFalse()
    {
        // Company-car requests must not consume motorcycle-only capacity.
        var slot = AvailableSlot.Create(ParkingSlotId.FromString("M1"), isMotorcycleCapacity: true);
        var companyCar = VehicleInformation.Create("CC-001", VehicleType.Sedan, false, false, isCompanyCar: true);
        Assert.False(slot.CanAccommodate(companyCar));
    }

    [Fact]
    public void CanAccommodate_CompanyCar_EVOnNonChargerReservedSlot_IsFalse()
    {
        var slot = AvailableSlot.Create(
            ParkingSlotId.FromString("CC1"),
            isCompanyCarReserved: true,
            reservedForUserId: "owner-1");
        var companyEv = VehicleInformation.Create("CC-EV-1", VehicleType.Sedan, isElectric: true, false, isCompanyCar: true);
        Assert.False(slot.CanAccommodate(companyEv));
    }

    [Fact]
    public void CanAccommodate_ElectricMotorcycle_OnMotorcycleSlotWithoutCharger_IsFalse()
    {
        // Existing charger rule still applies to motorcycle requests.
        var slot = AvailableSlot.Create(ParkingSlotId.FromString("M1"), isMotorcycleCapacity: true);
        var ev = VehicleInformation.Create("EV-MC", VehicleType.Motorcycle, isElectric: true, false, false);
        Assert.False(slot.CanAccommodate(ev));
    }

    private static VehicleInformation Motorcycle() =>
        VehicleInformation.Create("MC-001", VehicleType.Motorcycle, false, false, false);

    private static VehicleInformation Sedan() =>
        VehicleInformation.Create("CAR-001", VehicleType.Sedan, false, false, false);
}
