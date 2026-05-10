using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Commands;

public record CancelBookingCommand(
    Guid RequestId,
    string TenantId,
    string RequestorId,
    string Reason) : IRequest<CancelBookingResult>;
