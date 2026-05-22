using FPS.SharedKernel.DomainEvents;

namespace FPS.Booking.Application.Services;

public interface IBookingEventPublisher : IEventPublisher
{
    // Returns a publisher that wraps each domain event in the BookingEventEnvelope
    // and publishes to the stable "booking-events" topic with the given context.
    IEventPublisher WithContext(BookingPublishContext context);
}
