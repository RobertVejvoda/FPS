using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FPS.Domain.Entities;
using FPS.Application.Common.Interfaces;

namespace FPS.Application.Booking.Commands
{
    public class CreateBookingCommand : IRequest<int>
    {
        public string EmployeeId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ParkingSlotId { get; set; }
    }

    public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, int>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateBookingCommandHandler(IBookingRepository bookingRepository, IUnitOfWork unitOfWork)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
        {
            var booking = new BookingRequest
            {
                EmployeeId = request.EmployeeId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                ParkingSlotId = request.ParkingSlotId,
                Status = BookingStatus.Pending,
                CreatedDate = DateTime.UtcNow
            };

            _bookingRepository.Add(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return booking.Id;
        }
    }
}