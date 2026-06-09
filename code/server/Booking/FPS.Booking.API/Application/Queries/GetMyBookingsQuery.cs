using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetMyBookingsQuery(
    string TenantId,
    string RequestorId,
    DateOnly? From,
    DateOnly? To,
    string? Status,
    int PageSize,
    string? Cursor) : IRequest<BookingListResult>;
