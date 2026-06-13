using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetHrEmployeeHistoryQuery(
    string TenantId,
    string RequestorId,
    DateOnly? From,
    DateOnly? To,
    string? StatusFilter,
    int PageSize,
    string? Cursor) : IRequest<HrEmployeeHistoryResult>;
