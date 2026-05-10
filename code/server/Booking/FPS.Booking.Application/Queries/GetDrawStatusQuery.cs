using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetDrawStatusQuery(
    string TenantId,
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd) : IRequest<DrawStatusResult?>;
