using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Commands;

public record EvaluateNoShowCommand(
    string TenantId,
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason) : IRequest<EvaluateNoShowResult>;
