namespace FPS.Booking.Infrastructure.Tests.Repositories;

// Integration tests require TestContainers with MongoDB + Dapr sidecar.
// Tracked gaps — implement when MongoDB read-side is built:
//   - GetBookingRequestAsync: returns persisted dto by id
//   - UpdateBookingRequestStatusAsync: persists new status, verifies round-trip
//   - CountRequestsForDateAsync: counts by tenant + date
//   - HasOverlappingRequestAsync: detects overlapping time slots (needs MongoDB query)
public class BookingRepositoryTests
{
    [Fact(Skip = "Requires MongoDB + Dapr sidecar — implement with TestContainers")]
    public Task GetByIdAsync_ExistingBooking_ReturnsBooking() => Task.CompletedTask;

    [Fact(Skip = "Requires MongoDB + Dapr sidecar — implement with TestContainers")]
    public Task UpdateBookingRequestStatusAsync_PendingToCancelled_PersistsStatus() => Task.CompletedTask;

    [Fact(Skip = "Requires MongoDB + Dapr sidecar — implement with TestContainers")]
    public Task CountRequestsForDateAsync_ReturnsCorrectCount() => Task.CompletedTask;

    [Fact(Skip = "Requires MongoDB + Dapr sidecar — implement with TestContainers")]
    public Task HasOverlappingRequestAsync_OverlappingSlot_ReturnsTrue() => Task.CompletedTask;

    // B004 gaps — replace InMemoryEmployeeMetricsService with MongoDB-backed implementation
    [Fact(Skip = "Requires MongoDB — implement with TestContainers")]
    public Task EmployeeMetrics_IncrementAndQuery_ReturnsCorrectLookbackCount() => Task.CompletedTask;

    [Fact(Skip = "Requires MongoDB — implement with TestContainers")]
    public Task EmployeeMetrics_AllocationsOutsideLookbackWindow_NotCounted() => Task.CompletedTask;
}
