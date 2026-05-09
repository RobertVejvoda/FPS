using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using FPS.Application.Common.Interfaces;
using FPS.Application.Common.Exceptions;
using FPS.Domain.Entities;

namespace FPS.Application.Booking.Commands
{
    public class CancelBookingCommand : IRequest
    {
        public int BookingId { get; set; }
        public string EmployeeId { get; set; }
    }

    public class CancelBookingCommandHandler : IRequestHandler<CancelBookingCommand>
    {
        private readonly IBookingRepository _bookingRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CancelBookingCommandHandler(IBookingRepository bookingRepository, IUnitOfWork unitOfWork)
        {
            _bookingRepository = bookingRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
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

            booking.Status = BookingStatus.Cancelled;
            booking.UpdatedDate = DateTime.UtcNow;

            _bookingRepository.Update(booking);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Unit.Value;
        }
    }
}