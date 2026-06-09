namespace FPS.Booking.Domain.ValueObjects;

public class BookingRequestId : StronglyTypedId<Guid>
{
    private BookingRequestId(Guid value) : base(value) { }
    
    public static BookingRequestId New() => new(Guid.NewGuid());
    
    public static BookingRequestId FromGuid(Guid id)
    {
        if (id == Guid.Empty)
            throw new BookingException("BookingRequestId cannot be empty");
            
        return new BookingRequestId(id);
    }
    
    public static BookingRequestId FromString(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new BookingException("BookingRequestId cannot be empty");
            
        return new BookingRequestId(Guid.Parse(id));
    }
}