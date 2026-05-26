using FPS.Booking.Application.Models;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Domain.ValueObjects;
using MediatR;

namespace FPS.Booking.Application.Queries;

public record GetDrawLifecycleQuery(
    string TenantId,
    string LocationId,
    DateOnly Date,
    DateTime TimeSlotStart,
    DateTime TimeSlotEnd) : IRequest<DrawLifecycleResult?>;

public sealed class GetDrawLifecycleHandler(IDrawRepository drawRepository)
    : IRequestHandler<GetDrawLifecycleQuery, DrawLifecycleResult?>
{
    public async Task<DrawLifecycleResult?> Handle(GetDrawLifecycleQuery query, CancellationToken cancellationToken)
    {
        var timeSlot = TimeSlot.Create(query.TimeSlotStart, query.TimeSlotEnd);
        var drawKey = DrawKey.Create(query.TenantId, query.LocationId, query.Date, timeSlot);

        var attempt = await drawRepository.GetByKeyAsync(drawKey.ToStoreKey(), cancellationToken);
        if (attempt is null) return null;

        var steps = attempt.LifecycleSteps.Count > 0
            ? attempt.LifecycleSteps.Select(s => new DrawLifecycleStep(s.StepName, s.Status, s.Summary, s.StartedAt, s.ErrorMessage)).ToList()
            : DeriveSteps(attempt);
        var decisions = MapDecisions(attempt, query.Date);
        var tier2Refs = attempt.Tier2CandidateSequence
            .Select(id => FormatBookingRef(id, query.Date))
            .ToList();

        return new DrawLifecycleResult(
            DrawKey: attempt.DrawKey,
            LocationId: attempt.LocationId,
            Date: attempt.Date,
            Status: attempt.Status,
            AlgorithmVersion: attempt.AlgorithmVersion,
            Seed: attempt.Seed,
            AuditReference: attempt.DrawKey,
            RequestCount: attempt.Decisions.Count,
            AllocatedCount: attempt.AllocatedCount,
            RejectedCount: attempt.RejectedCount,
            WaitlistedCount: attempt.WaitlistedCount,
            StartedAt: attempt.StartedAt,
            CompletedAt: attempt.CompletedAt,
            Steps: steps,
            Decisions: decisions,
            Tier2CandidateSequence: tier2Refs);
    }

    private static List<DrawLifecycleStep> DeriveSteps(DrawAttemptDto attempt)
    {
        var isCompleted = attempt.Status == "Completed";
        var hasDecisions = attempt.Decisions.Count > 0;
        var hasAlgorithm = !string.IsNullOrEmpty(attempt.AlgorithmVersion);
        var startedAt = attempt.StartedAt == default ? (DateTime?)null : attempt.StartedAt;
        var completedAt = attempt.CompletedAt;

        return
        [
            Step("DrawStarted",
                startedAt.HasValue ? "Completed" : "Unknown",
                $"Seed: {attempt.Seed}",
                startedAt),

            Step("RequestsLoaded",
                hasDecisions ? "Completed" : (isCompleted ? "Completed" : "Unknown"),
                hasDecisions ? $"{attempt.Decisions.Count} request(s) loaded for evaluation" : "No requests found",
                startedAt),

            Step("PolicyResolved",
                hasAlgorithm ? "Completed" : "Unknown",
                hasAlgorithm ? $"Algorithm version: {attempt.AlgorithmVersion}" : null,
                startedAt),

            Step("EligibilityFiltered",
                hasDecisions ? "Completed" : (isCompleted ? "Completed" : "NotReached"),
                null,
                startedAt),

            Step("CompanyCarTierEvaluated",
                hasDecisions ? "Completed" : (isCompleted ? "Completed" : "NotReached"),
                null,
                startedAt),

            Step("WeightedAllocationCompleted",
                hasDecisions ? "Completed" : (isCompleted ? "Completed" : "NotReached"),
                hasDecisions
                    ? $"{attempt.AllocatedCount} allocated, {attempt.RejectedCount} rejected, {attempt.WaitlistedCount} waitlisted"
                    : null,
                startedAt),

            Step("DecisionsPersisted",
                isCompleted ? "Completed" : "NotReached",
                null,
                completedAt),

            Step("EventsPublished",
                isCompleted ? "Attempted" : "NotReached",
                isCompleted ? "Derived from legacy attempt; delivery not guaranteed (fire-and-forget)" : null,
                completedAt),

            Step("DrawCompleted",
                isCompleted ? "Completed" : "NotReached",
                null,
                completedAt),
        ];
    }

    private static List<DrawLifecycleDecision> MapDecisions(DrawAttemptDto attempt, DateOnly date)
        => attempt.Decisions.Select(d => new DrawLifecycleDecision(
            BookingReference: FormatBookingRef(d.RequestId, date),
            Outcome: d.Outcome,
            SlotReference: FormatSlotRef(d.SlotId),
            Reason: d.Reason)).ToList();

    private static string FormatBookingRef(string requestId, DateOnly date)
    {
        if (string.IsNullOrEmpty(requestId)) return "BK-UNKNOWN";
        var datePart = date.ToString("yyyyMMdd");
        var cleaned = requestId.Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        var shortCode = cleaned.Length >= 4 ? cleaned[^4..] : cleaned.PadLeft(4, '0');
        return $"BK-{datePart}-{shortCode}";
    }

    private static string? FormatSlotRef(string? slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return null;
        var isGuid = Guid.TryParse(slotId, out _);
        return isGuid ? "Assigned space" : slotId.Replace("LOC-MAIN-", "Space ", StringComparison.OrdinalIgnoreCase);
    }

    private static DrawLifecycleStep Step(string name, string status, string? summary, DateTime? occurredAt)
        => new(name, status, summary, occurredAt);
}
