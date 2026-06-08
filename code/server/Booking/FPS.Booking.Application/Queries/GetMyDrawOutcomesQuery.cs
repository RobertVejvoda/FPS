using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetMyDrawOutcomesQuery(
    string TenantId,
    string RequestorId,
    DateOnly From,
    DateOnly To) : IRequest<IReadOnlyList<MyDrawOutcomeSummary>>;
