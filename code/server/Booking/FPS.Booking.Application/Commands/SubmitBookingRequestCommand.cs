using FPS.Booking.Application.Domain;
using FPS.Booking.Application.Models;
using FPS.Booking.Application.Specifications;
using FPS.SharedKernel.Interfaces;
using MediatR;

namespace FPS.Booking.Application.Commands
{
    public class SubmitBookingRequestCommand : IRequest<BookingResponse>
    {
        public required string CustomerId { get; set; }
        public required string Description { get; set; }
        public DateTime RequestedDate { get; set; }
        public TimeSpan Duration { get; set; }
        public required string ServiceType { get; set; }
        public required string Location { get; set; }
    }

    public class SubmitBookingRequestCommandHandler : IRequestHandler<SubmitBookingRequestCommand, BookingResponse>
    {
        private readonly IRepository<BookingRequest, BookingRequestId> bookingRequestRepository;

        public SubmitBookingRequestCommandHandler(IRepository<BookingRequest, BookingRequestId> bookingRequestRepository)
        {
            this.bookingRequestRepository = bookingRequestRepository;
        }

        public async Task<BookingResponse> Handle(SubmitBookingRequestCommand request, CancellationToken cancellationToken)
        {
            // Validate the request
            // Check if there's an existing booking request for the same customer, date and time
            var existingBookings = await bookingRequestRepository
                .ListAsync(new BookingRequestByCustomerAndTimeSpecification(
                    request.CustomerId, 
                    request.RequestedDate, 
                    request.RequestedDate.Add(request.Duration)),
                cancellationToken);

            if (existingBookings.Any())
            {
                throw new ApplicationException("A booking request already exists for this time period");
            }

            // Additional validation
            if (request.RequestedDate < DateTime.UtcNow)
            {
                throw new ApplicationException("Booking date must be in the future");
            }

            if (request.Duration <= TimeSpan.Zero)
            {
                throw new ApplicationException("Duration must be positive");
            }

            // Create a new booking request entity
            var bookingRequestId = BookingRequestId.New();
            var bookingRequest = new BookingRequest(bookingRequestId, request.CustomerId, request.Description, request.RequestedDate, request.Duration, request.ServiceType, request.Location, "Pending", DateTime.UtcNow);

            // Save the booking request
            await bookingRequestRepository.AddAsync(bookingRequest, cancellationToken);

            return new BookingResponse(bookingRequestId.Value.ToString(), "Pending", DateTime.UtcNow, "Booking request submitted successfully");
        }
    }
}