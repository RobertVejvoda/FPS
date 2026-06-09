using FPS.Booking.Application.Models;

namespace FPS.Booking.Application.Workflows;

// Serializable slot data passed between workflow activities.
public sealed record SlotData(
    string SlotId,
    bool HasCharger,
    bool IsAccessible,
    bool IsCompanyCarReserved);

// Serializable employee metrics snapshot passed between workflow activities.
public sealed record EmployeeMetricsData(
    string RequestorId,
    int RecentAllocationCount,
    int ActivePenaltyScore);

// Output of RunAllocationActivity.
public sealed record AllocationResult(
    List<DrawDecisionDto> Decisions,
    List<string> Tier2CandidateSequence,
    string AlgorithmVersion,
    int AllocatedCount,
    int RejectedCount,
    int WaitlistedCount);

// Input for activities that need the full draw attempt context.
public sealed record DrawAttemptContext(
    string DrawKey,
    string TenantId,
    string LocationId,
    string Date,
    long Seed,
    string StartedAt);  // ISO8601 UTC

// Output of ResolveDrawInputActivity.
public sealed record ResolvedDrawInput(
    string DrawKey,
    long Seed,
    int AllocationLookbackDays);

// Output of LoadPendingRequestsActivity.
public sealed record PendingRequestsResult(List<BookingRequestDto> Requests);

// Output of LoadCapacityActivity.
public sealed record CapacityResult(List<SlotData> Slots);

// Output of LoadMetricsActivity.
public sealed record MetricsResult(List<EmployeeMetricsData> Metrics);
