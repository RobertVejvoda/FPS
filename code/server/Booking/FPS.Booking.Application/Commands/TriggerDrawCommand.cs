using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Commands;

public record TriggerDrawCommand(
    string TenantId,
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd,
    string Reason) : IRequest<TriggerDrawResult>;
