using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetHrSlotHistoryQuery(
    string TenantId,
    string? LocationId,
    string SlotId,
    DateOnly? From,
    DateOnly? To,
    int PageSize,
    string? Cursor) : IRequest<HrSlotHistoryResult>;
