using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FPS.Application.Common.Interfaces;
using FPS.Application.Common.Exceptions;
using FPS.Domain.Entities;

namespace FPS.Application.Booking.Commands
{
    public class ModifyBookingCommand : IRequest
    {
        public int BookingId { get; set; }
        public string EmployeeId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? ParkingSlotId { get; set; }
    }

    public class ModifyBookingCommandHandler : IRequestHandler<ModifyBookingCommand>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ModifyBookingCommandHandler(IBookingRepository bookingRepository, IUnitOfWork unitOfWork)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(ModifyBookingCommand request, CancellationToken cancellationToken)
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

            if (booking.Status == BookingStatus.Cancelled)
            {
                throw new InvalidOperationException("Cannot modify a cancelled booking");
            }

            if (request.StartTime.HasValue)
                booking.StartTime = request.StartTime.Value;
            
            if (request.EndTime.HasValue)
                booking.EndTime = request.EndTime.Value;
            
            if (request.ParkingSlotId.HasValue)
                booking.ParkingSlotId = request.ParkingSlotId.Value;
            
            booking.Status = BookingStatus.Modified;
            booking.UpdatedDate = DateTime.UtcNow;

            _bookingRepository.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}