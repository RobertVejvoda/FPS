namespace FPS.Booking.Domain.ValueObjects;

// Draw-time snapshot of allocation history and penalties for one employee.
// Immutable — the Draw uses these values as-of the draw moment; later updates
// do not retroactively change already-completed Draw outcomes.
public sealed record EmployeeMetrics(
    string RequestorId,
    int RecentAllocationCount,
    int ActivePenaltyScore)
{
    // Tier2Weight = 1 / (1 + RecentAllocationCount + ActivePenaltyScore)
    // Higher denominator = lower weight = lower draw probability.
    public double Tier2Weight => 1.0 / (1 + RecentAllocationCount + ActivePenaltyScore);
}
