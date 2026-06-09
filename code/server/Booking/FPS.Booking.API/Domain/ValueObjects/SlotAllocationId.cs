using FPS.SharedKernel.ValueObjects;

namespace FPS.Booking.Domain.ValueObjects;

public class SlotAllocationId : StronglyTypedId<Guid>
{
    private SlotAllocationId(Guid value) : base(value) { }
    
    public static SlotAllocationId New() => new(Guid.NewGuid());
    
    public static SlotAllocationId FromGuid(Guid id)
    {
        if (id == Guid.Empty)
            throw new BookingException("SlotAllocationId cannot be empty");
            
        return new SlotAllocationId(id);
    }
    
    public static SlotAllocationId FromString(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new BookingException("SlotAllocationId cannot be empty");
            
        return new SlotAllocationId(Guid.Parse(id));
    }
}