using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetHrBookingListQuery(
    string TenantId,
    DateOnly? From,
    DateOnly? To,
    string? StatusFilter,
    int PageSize,
    string? Cursor) : IRequest<HrBookingListResult>;
