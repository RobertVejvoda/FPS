using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetHrDrawOutcomesQuery(
    string TenantId,
    string? LocationId,
    DateOnly From,
    DateOnly To) : IRequest<IReadOnlyList<HrDrawOutcomeSummary>>;
