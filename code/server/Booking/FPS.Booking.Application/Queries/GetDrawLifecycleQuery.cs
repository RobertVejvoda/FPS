using FPS.Booking.Application.Models;
using MediatR;

namespace FPS.Booking.Application.Queries;

/// <summary>
/// Query to retrieve the full lifecycle view of a Draw for auditors and authorized administrators.
/// Returns step-level tracking, per-booking decisions, and deterministic evidence.
/// </summary>
public record GetDrawLifecycleQuery(
    string TenantId,
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd) : IRequest<DrawLifecycleResult?>;
