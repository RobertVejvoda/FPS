using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Queries;

/// <summary>
/// Handler for GetDrawLifecycleQuery.
/// Returns the complete Draw lifecycle with step-level tracking for auditors/administrators.
/// </summary>
public sealed class GetDrawLifecycleHandler : IRequestHandler<GetDrawLifecycleQuery, DrawLifecycleResult?>
{
    private readonly IDrawRepository drawRepository;

    public GetDrawLifecycleHandler(IDrawRepository drawRepository)
    {
        ArgumentNullException.ThrowIfNull(drawRepository);
        this.drawRepository = drawRepository;
    }

    public async Task<DrawLifecycleResult?> Handle(GetDrawLifecycleQuery query, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(query.TimeSlotStart, query.TimeSlotEnd);
        var drawKey = DrawKey.Create(query.TenantId, query.LocationId, query.Date, timeSlot);

        var attempt = await drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);
        if (attempt is null) return null;

        return new DrawLifecycleResult
        {
            DrawKey = attempt.DrawKey,
            TenantId = attempt.TenantId,
            LocationId = attempt.LocationId,
            Date = attempt.Date,
            Status = attempt.Status,
            Seed = attempt.Seed,
            AlgorithmVersion = attempt.AlgorithmVersion,
            StartedAt = attempt.StartedAt,
            CompletedAt = attempt.CompletedAt,
            Steps = attempt.Steps,
            AllocatedCount = attempt.AllocatedCount,
            RejectedCount = attempt.RejectedCount,
            WaitlistedCount = attempt.WaitlistedCount,
            Decisions = attempt.Decisions,
            Tier2CandidateSequence = attempt.Tier2CandidateSequence,
            CorrelationId = attempt.CorrelationId,
            TraceId = attempt.TraceId
        };
    }
}
