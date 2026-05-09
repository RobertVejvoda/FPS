using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FPS.Application.Common.Interfaces;
using FPS.Application.Common.Exceptions;
using FPS.Domain.Entities;

namespace FPS.Application.Booking.Queries
{
    public class GetBookingDetailsQuery : IRequest<BookingDetailsDto>
    {
        public int BookingId { get; set; }
        public string EmployeeId { get; set; }
    }

    public class BookingDetailsDto
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; }
        public System.DateTime StartTime { get; set; }
        public System.DateTime EndTime { get; set; }
        public int ParkingSlotId { get; set; }
        public string Status { get; set; }
        public System.DateTime CreatedDate { get; set; }
        public System.DateTime? UpdatedDate { get; set; }
    }

    public class GetBookingDetailsQueryHandler : IRequestHandler<GetBookingDetailsQuery, BookingDetailsDto>
    {
        private readonly IBookingRepository _bookingRepository;

        public GetBookingDetailsQueryHandler(IBookingRepository bookingRepository)
        {
            _bookingRepository = bookingRepository;
        }

        public async Task<BookingDetailsDto> Handle(GetBookingDetailsQuery request, CancellationToken cancellationToken)
        {
            var booking = await _bookingRepository.GetByIdAsync(request.BookingId);

            if (booking == null)
            {
                throw new NotFoundException(nameof(BookingRequest), request.BookingId);
            }

            if (booking.EmployeeId != request.EmployeeId)
            {
                throw new ForbiddenAccessException();
            }

            return new BookingDetailsDto
            {
                Id = booking.Id,
                EmployeeId = booking.EmployeeId,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                ParkingSlotId = booking.ParkingSlotId,
                Status = booking.Status.ToString(),
                CreatedDate = booking.CreatedDate,
                UpdatedDate = booking.UpdatedDate
            };
        }
    }
}