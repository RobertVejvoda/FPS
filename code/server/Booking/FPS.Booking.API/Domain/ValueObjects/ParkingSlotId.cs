using FPS.SharedKernel.ValueObjects;

namespace FPS.Booking.Domain.ValueObjects;

public class ParkingSlotId : StronglyTypedId<string>
{
    private ParkingSlotId(string value) : base(value) { }
    
    public static ParkingSlotId FromString(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new BookingException("ParkingSlotId cannot be empty");
            
        return new ParkingSlotId(id);
    }
}