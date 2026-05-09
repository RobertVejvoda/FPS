namespace FPS.Booking.Infrastructure.Tests.Repositories;

// Integration tests for repository implementations will use TestContainers with MongoDB.
// These will be implemented in Phase 1 alongside the actual repository implementation.
public class BookingRepositoryTests
{
    [Fact(Skip = "Requires MongoDB — implement in Phase 1 with TestContainers")]
    public Task GetByIdAsync_ExistingBooking_ReturnsBooking()
    {
        return Task.CompletedTask;
    }
}
