using FPS.Booking.Domain.Aggregates.BookingRequestAggregate;
using FPS.Booking.Domain.Entities;

namespace FPS.Booking.Domain.Services;

public class DrawService
{
    // Processes all pending requests against available slots for a given draw date.
    // Returns one DrawDecision per request. Modifies profiles in-place (caller persists them).
    public IReadOnlyList<DrawDecision> RunDraw(
        IReadOnlyList<BookingRequest> pendingRequests,
        IReadOnlyList<AvailableSlot> availableSlots,
        IDictionary<UserId, EmployeeDrawProfile> profiles,
        DrawConfiguration config,
        DateTime drawDate,
        Random? random = null)
    {
        if (pendingRequests is null) throw new ArgumentNullException(nameof(pendingRequests));
        if (availableSlots is null) throw new ArgumentNullException(nameof(availableSlots));
        if (profiles is null) throw new ArgumentNullException(nameof(profiles));
        if (config is null) throw new ArgumentNullException(nameof(config));

        random ??= Random.Shared;

        var decisions = new List<DrawDecision>(pendingRequests.Count);
        var remainingSlots = availableSlots.ToList();
        var remainingRequests = pendingRequests.ToList();

        // Tier 1: guaranteed allocation for company car requests
        var companyCarRequests = remainingRequests
            .Where(r => r.Vehicle.IsCompanyCar)
            .ToList();

        foreach (var request in companyCarRequests)
        {
            var slot = FindBestSlot(request.Vehicle, remainingSlots);
            if (slot is not null)
            {
                decisions.Add(DrawDecision.Allocated(request.Id, request.RequestorId, slot.SlotId));
                remainingSlots.Remove(slot);
                UpdateProfile(profiles, request.RequestorId, won: true, drawDate, config);
            }
            else
            {
                decisions.Add(DrawDecision.Rejected(request.Id, request.RequestorId));
                UpdateProfile(profiles, request.RequestorId, won: false, drawDate, config);
            }
            remainingRequests.Remove(request);
        }

        // Tier 2: weighted lottery for remaining requests
        // Process slots one by one, picking a compatible weighted winner each round
        while (remainingSlots.Count > 0 && remainingRequests.Count > 0)
        {
            var slot = remainingSlots[0];
            var compatible = remainingRequests
                .Where(r => slot.CanAccommodate(r.Vehicle))
                .ToList();

            if (compatible.Count == 0)
            {
                // No compatible request for this slot — skip it
                remainingSlots.RemoveAt(0);
                continue;
            }

            var winner = PickWeightedWinner(compatible, profiles, config, random);
            decisions.Add(DrawDecision.Allocated(winner.Id, winner.RequestorId, slot.SlotId));
            remainingSlots.RemoveAt(0);
            remainingRequests.Remove(winner);
            UpdateProfile(profiles, winner.RequestorId, won: true, drawDate, config);
        }

        // Reject all remaining unallocated requests
        foreach (var request in remainingRequests)
        {
            decisions.Add(DrawDecision.Rejected(request.Id, request.RequestorId));
            UpdateProfile(profiles, request.RequestorId, won: false, drawDate, config);
        }

        return decisions;
    }

    private static AvailableSlot? FindBestSlot(VehicleInformation vehicle, List<AvailableSlot> slots)
    {
        // Prefer the most specific compatible slot to avoid wasting specialised spots
        return slots
            .Where(s => s.CanAccommodate(vehicle))
            .OrderByDescending(s => SpecificityScore(s, vehicle))
            .FirstOrDefault();
    }

    private static int SpecificityScore(AvailableSlot slot, VehicleInformation vehicle)
    {
        var score = 0;
        if (slot.IsCompanyCarReserved && vehicle.IsCompanyCar) score += 4;
        if (slot.IsAccessible && vehicle.RequiresAccessibleSpot) score += 2;
        if (slot.HasCharger && vehicle.IsElectric) score += 1;
        return score;
    }

    private static BookingRequest PickWeightedWinner(
        List<BookingRequest> candidates,
        IDictionary<UserId, EmployeeDrawProfile> profiles,
        DrawConfiguration config,
        Random random)
    {
        // Guaranteed-win candidates go first (any one of them wins immediately)
        var guaranteed = candidates
            .Where(r => GetProfile(profiles, r.RequestorId).IsGuaranteedWin(config.GuaranteedWinThreshold))
            .ToList();

        if (guaranteed.Count > 0)
            return guaranteed[random.Next(guaranteed.Count)];

        // Weighted random selection
        var weights = candidates
            .Select(r => (Request: r, Weight: (double)GetProfile(profiles, r.RequestorId).CalculateWeight(config.MaxFailedDrawCap)))
            .ToList();

        var total = weights.Sum(w => w.Weight);
        var roll = random.NextDouble() * total;
        var cumulative = 0.0;

        foreach (var (request, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative)
                return request;
        }

        return candidates[^1];
    }

    private static void UpdateProfile(
        IDictionary<UserId, EmployeeDrawProfile> profiles,
        UserId employeeId,
        bool won,
        DateTime drawDate,
        DrawConfiguration config)
    {
        var profile = GetProfile(profiles, employeeId);
        if (won)
            profile.RecordWin(drawDate);
        else
            profile.RecordLoss(drawDate);
    }

    private static EmployeeDrawProfile GetProfile(IDictionary<UserId, EmployeeDrawProfile> profiles, UserId employeeId)
    {
        if (!profiles.TryGetValue(employeeId, out var profile))
        {
            profile = EmployeeDrawProfile.Create(employeeId);
            profiles[employeeId] = profile;
        }
        return profile;
    }
}
