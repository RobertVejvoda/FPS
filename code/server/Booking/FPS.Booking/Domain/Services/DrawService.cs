using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;

namespace FPS.Booking.Domain.Services;

public sealed class DrawService
{
    private const string AlgorithmVersion = "1.0";

    // Executes one Draw for a given set of pending requests and available slots.
    // Random is injected so tests can use a fixed seed for deterministic outcomes.
    // Modifies no external state — caller is responsible for persisting decisions.
    public DrawResult RunDraw(
        IReadOnlyList<BookingRequest> pendingRequests,
        IReadOnlyList<AvailableSlot> availableSlots,
        IReadOnlyDictionary<string, EmployeeMetrics> metrics,
        long seed)
    {
        var rng = new Random((int)(seed & 0x7FFFFFFF));
        var decisions = new List<DrawDecision>(pendingRequests.Count);
        var remainingSlots = availableSlots.ToList();

        // Tier 1: company-car requests — guaranteed or rejected on overflow
        var tier1 = pendingRequests.Where(r => r.Vehicle.IsCompanyCar).ToList();
        var tier2 = pendingRequests.Where(r => !r.Vehicle.IsCompanyCar).ToList();

        foreach (var request in tier1)
        {
            var fixedSlotResult = CompanyCarReservedSlotRules.Resolve(
                request.RequestorId,
                request.Vehicle,
                availableSlots,
                slot => remainingSlots.Any(s => s.SlotId == slot.SlotId));

            if (fixedSlotResult.Slot is not null)
            {
                decisions.Add(DrawDecision.Allocated(request.Id, request.RequestorId, fixedSlotResult.Slot.SlotId));
                remainingSlots.RemoveAll(s => s.SlotId == fixedSlotResult.Slot.SlotId);
            }
            else
            {
                decisions.Add(DrawDecision.Rejected(request.Id, request.RequestorId,
                    fixedSlotResult.RejectionReason ?? CompanyCarReservedSlotRules.MissingReservedSlotReason));
            }
        }

        // Tier 2: weighted lottery — losers due to capacity exhaustion are Waitlisted, not Rejected.
        // Sort motorcycle-only slots first so motorcycle preference holds: motorcycles take motorcycle
        // units before competing with cars for ordinary slots. Stable for everything else.
        remainingSlots = remainingSlots
            .OrderByDescending(s => s.IsMotorcycleCapacity)
            .ToList();

        var remaining = tier2.ToList();
        while (remaining.Count > 0 && remainingSlots.Count > 0)
        {
            var slot = remainingSlots[0];
            var compatible = string.IsNullOrWhiteSpace(slot.ReservedForUserId)
                ? remaining.Where(r => slot.CanAccommodate(r.Vehicle)).ToList()
                : [];

            if (compatible.Count == 0)
            {
                remainingSlots.RemoveAt(0);
                continue;
            }

            var winner = PickWeightedWinner(compatible, metrics, rng);
            decisions.Add(DrawDecision.Allocated(winner.Id, winner.RequestorId, slot.SlotId));
            remainingSlots.RemoveAt(0);
            remaining.Remove(winner);
        }

        // Remaining Tier 2 with no slot available → Waitlisted (eligible but capacity exhausted)
        foreach (var request in remaining)
            decisions.Add(DrawDecision.Waitlisted(request.Id, request.RequestorId));

        return new DrawResult(
            AlgorithmVersion,
            seed,
            decisions,
            // Preserve ordered Tier 2 candidate sequence for B005 reallocation
            tier2.Select(r => r.Id).ToList());
    }

    private static BookingRequest PickWeightedWinner(
        List<BookingRequest> candidates,
        IReadOnlyDictionary<string, EmployeeMetrics> metrics,
        Random rng)
    {
        var weights = candidates
            .Select(r => (Request: r, Weight: GetMetrics(metrics, r.RequestorId.Value.ToString()).Tier2Weight))
            .ToList();

        var total = weights.Sum(w => w.Weight);
        var roll = rng.NextDouble() * total;
        var cumulative = 0.0;

        foreach (var (request, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative) return request;
        }

        return candidates[^1];
    }

    private static EmployeeMetrics GetMetrics(IReadOnlyDictionary<string, EmployeeMetrics> metrics, string requestorId)
        => metrics.TryGetValue(requestorId, out var m) ? m : new EmployeeMetrics(requestorId, 0, 0);
}

public sealed record DrawResult(
    string AlgorithmVersion,
    long Seed,
    IReadOnlyList<DrawDecision> Decisions,
    IReadOnlyList<BookingRequestId> Tier2CandidateSequence);
