using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FPS.Application.Common.Interfaces;
using FPS.Domain.Entities;

namespace FPS.Application.Booking.Queries
{
    public class GetEmployeeBookingsQuery : IRequest<List<BookingSummaryDto>>
    {
        public string EmployeeId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class BookingSummaryDto
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ParkingSlotId { get; set; }
        public string Status { get; set; }
    }

    public class GetEmployeeBookingsQueryHandler : IRequestHandler<GetEmployeeBookingsQuery, List<BookingSummaryDto>>
    {
        private readonly IBookingRepository _bookingRepository;

        public GetEmployeeBookingsQueryHandler(IBookingRepository bookingRepository)
        {
            _bookingRepository = bookingRepository;
        }

        public async Task<List<BookingSummaryDto>> Handle(GetEmployeeBookingsQuery request, CancellationToken cancellationToken)
        {
            var bookings = await _bookingRepository.GetBookingsByEmployeeIdAsync(
                request.EmployeeId, 
                request.FromDate, 
                request.ToDate);

            return bookings.Select(b => new BookingSummaryDto
            {
                Id = b.Id,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                ParkingSlotId = b.ParkingSlotId,
                Status = b.Status.ToString()
            }).ToList();
        }
    }
}