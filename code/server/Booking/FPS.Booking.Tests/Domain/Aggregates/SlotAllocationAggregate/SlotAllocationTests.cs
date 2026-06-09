namespace FPS.Booking.Domain.Tests.Aggregates.SlotAllocationAggregate;

public class SlotAllocationTests
{
    [Fact]
    public void CreateAllocation_WithValidParameters_ReturnsAllocationWithReservedStatus()
    {
        // Arrange
        var bookingRequestId = BookingRequestId.New();
        var slotId = ParkingSlotId.FromString("A101");
        var period = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        var eventPublisher = new Mock<IEventPublisher>();

        // Act
        var allocation = SlotAllocation.CreateAllocation(bookingRequestId, slotId, period, eventPublisher.Object);

        // Assert
        Assert.NotNull(allocation);
        Assert.Equal(bookingRequestId, allocation.BookingRequestId);
        Assert.Equal(slotId, allocation.SlotId);
        Assert.Equal(period, allocation.Period);
        Assert.Equal(SlotAllocationStatus.Reserved, allocation.Status);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<SlotAllocationCreatedEvent>(e =>
            e.AllocationId == allocation.Id &&
            e.RequestId == bookingRequestId &&
            e.SlotId == slotId &&
            e.Period == period), default), Times.Once);
    }

    [Fact]
    public void StartUsage_WhenReserved_ChangesStatusToInUse()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        var startTime = DateTime.UtcNow.AddDays(1).AddHours(0.5); // Within period

        // Act
        allocation.StartUsage(startTime, eventPublisher.Object);

        // Assert
        Assert.Equal(SlotAllocationStatus.InUse, allocation.Status);
        Assert.Equal(startTime, allocation.UsageStartTime);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<SlotUsageStartedEvent>(e =>
            e.AllocationId == allocation.Id &&
            e.StartTime == startTime), default), Times.Once);
    }

    [Fact]
    public void StartUsage_WithTimeOutsidePeriod_ThrowsBookingException()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        var startTime = DateTime.UtcNow; // Outside period

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => allocation.StartUsage(startTime, eventPublisher.Object));
        Assert.Equal("Start time must be within the allocated period", exception.Message);
    }

    [Fact]
    public void StartUsage_WhenNotReserved_ThrowsBookingException()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        allocation.Cancel("User cancelled", eventPublisher.Object);
        var startTime = DateTime.UtcNow.AddDays(1).AddHours(0.5);

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => allocation.StartUsage(startTime, eventPublisher.Object));
        Assert.Equal("Only reserved slots can be used", exception.Message);
    }

    [Fact]
    public void CompleteUsage_WhenInUse_ChangesStatusToCompleted()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        var startTime = DateTime.UtcNow.AddDays(1).AddHours(0.5);
        var endTime = startTime.AddHours(1);
        allocation.StartUsage(startTime, eventPublisher.Object);

        // Act
        allocation.CompleteUsage(endTime, eventPublisher.Object);

        // Assert
        Assert.Equal(SlotAllocationStatus.Completed, allocation.Status);
        Assert.Equal(endTime, allocation.UsageEndTime);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<SlotUsageCompletedEvent>(e =>
            e.AllocationId == allocation.Id &&
            e.EndTime == endTime), default), Times.Once);
    }

    [Fact]
    public void CompleteUsage_WhenNotInUse_ThrowsBookingException()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        var endTime = DateTime.UtcNow.AddDays(1).AddHours(1.5);

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => allocation.CompleteUsage(endTime, eventPublisher.Object));
        Assert.Equal("Only slots in use can be completed", exception.Message);
    }

    [Fact]
    public void CompleteUsage_WithEndTimeBeforeStartTime_ThrowsBookingException()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        var startTime = DateTime.UtcNow.AddDays(1).AddHours(1);
        allocation.StartUsage(startTime, eventPublisher.Object);
        var endTime = startTime.AddMinutes(-30); // Before start time

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => allocation.CompleteUsage(endTime, eventPublisher.Object));
        Assert.Equal("End time cannot be before start time", exception.Message);
    }

    [Fact]
    public void Cancel_WhenReserved_ChangesStatusToCancelled()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        var reason = "User cancelled";

        // Act
        allocation.Cancel(reason, eventPublisher.Object);

        // Assert
        Assert.Equal(SlotAllocationStatus.Cancelled, allocation.Status);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<SlotAllocationCancelledEvent>(e =>
            e.AllocationId == allocation.Id &&
            e.Reason == reason), default), Times.Once);
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsBookingException()
    {
        // Arrange
        var allocation = CreateReservedAllocation(out var eventPublisher);
        var startTime = DateTime.UtcNow.AddDays(1).AddHours(0.5);
        var endTime = startTime.AddHours(1);
        allocation.StartUsage(startTime, eventPublisher.Object);
        allocation.CompleteUsage(endTime, eventPublisher.Object);

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => allocation.Cancel("Some reason", eventPublisher.Object));
        Assert.Equal("Completed allocations cannot be cancelled", exception.Message);
    }

    // Helper method to create a reserved allocation
    private SlotAllocation CreateReservedAllocation(out Mock<IEventPublisher> eventPublisher)
    {
        var bookingRequestId = BookingRequestId.New();
        var slotId = ParkingSlotId.FromString("A101");
        var period = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        eventPublisher = new Mock<IEventPublisher>();

        return SlotAllocation.CreateAllocation(bookingRequestId, slotId, period, eventPublisher.Object);
    }
}