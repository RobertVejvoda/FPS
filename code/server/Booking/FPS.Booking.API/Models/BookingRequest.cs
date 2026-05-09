using FPS.Booking.API.Models;
using FPS.SharedKernel.Interfaces;

namespace FPS.Booking.Application.Models;

public record BookingRequest(BookingRequestId Id, string CustomerId, string Description, DateTime RequestedDate, TimeSpan Duration, string ServiceType, string Location, string Status, DateTime CreatedAt) : IEntity<BookingRequestId>
{
    public BookingRequestId GetId() => Id;
}