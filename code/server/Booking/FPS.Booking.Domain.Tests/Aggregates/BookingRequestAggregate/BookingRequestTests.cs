namespace FPS.Booking.Domain.Tests.Aggregates.BookingRequestAggregate;

public class BookingRequestTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsRequestWithPendingStatus()
    {
        // Arrange
        var userId = UserId.New();
        var period = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        var vehicle = VehicleInformation.Create("ABC123", VehicleType.Sedan, false, false, true); // Updated to include IsCompanyCar
        var eventPublisher = new Mock<IEventPublisher>();

        // Act
        var request = BookingRequest.Create(userId, period, vehicle, eventPublisher.Object);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(BookingRequestStatus.Pending, request.Status);
        Assert.Equal(userId, request.RequestorId);
        Assert.Equal(period, request.RequestedPeriod);
        Assert.Equal(vehicle, request.Vehicle);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<BookingRequestCreatedEvent>(e =>
            e.RequestId == request.Id &&
            e.RequestorId == userId &&
            e.RequestedPeriod == period), default), Times.Once);
    }

    [Fact]
    public void Accept_WhenPending_ChangesStatusToAccepted()
    {
        // Arrange
        var request = CreatePendingRequest(out var eventPublisher);

        // Act
        request.Accept(eventPublisher.Object);

        // Assert
        Assert.Equal(BookingRequestStatus.Accepted, request.Status);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<BookingRequestAcceptedEvent>(e =>
            e.RequestId == request.Id), default), Times.Once);
    }

    [Fact]
    public void Accept_WhenNotPending_ThrowsBookingException()
    {
        // Arrange
        var request = CreatePendingRequest(out var eventPublisher);
        request.Reject("No slots available", eventPublisher.Object);

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => request.Accept(eventPublisher.Object));
        Assert.Equal("Only pending requests can be accepted", exception.Message);
    }

    [Fact]
    public void Reject_WhenPending_ChangesStatusToRejected()
    {
        // Arrange
        var request = CreatePendingRequest(out var eventPublisher);
        var reason = "No slots available";

        // Act
        request.Reject(reason, eventPublisher.Object);

        // Assert
        Assert.Equal(BookingRequestStatus.Rejected, request.Status);
        Assert.Equal(reason, request.RejectionReason);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<BookingRequestRejectedEvent>(e =>
            e.RequestId == request.Id &&
            e.Reason == reason), default), Times.Once);
    }

    [Fact]
    public void Reject_WhenNotPending_ThrowsBookingException()
    {
        // Arrange
        var request = CreatePendingRequest(out var eventPublisher);
        request.Accept(eventPublisher.Object);

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => request.Reject("Some reason", eventPublisher.Object));
        Assert.Equal("Only pending requests can be rejected", exception.Message);
    }

    [Fact]
    public void Cancel_WhenPending_ChangesStatusToCancelled()
    {
        // Arrange
        var request = CreatePendingRequest(out var eventPublisher);
        var reason = "User cancelled";

        // Act
        request.Cancel(reason, eventPublisher.Object);

        // Assert
        Assert.Equal(BookingRequestStatus.Cancelled, request.Status);

        // Verify domain event was published
        eventPublisher.Verify(p => p.PublishAsync(It.Is<BookingRequestCancelledEvent>(e =>
            e.RequestId == request.Id &&
            e.Reason == reason), default), Times.Once);
    }

    [Fact]
    public void Cancel_WhenRejected_ThrowsBookingException()
    {
        // Arrange
        var request = CreatePendingRequest(out var eventPublisher);
        request.Reject("No slots available", eventPublisher.Object);

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() => request.Cancel("User cancelled", eventPublisher.Object));
        Assert.Equal("Only pending or accepted requests can be cancelled", exception.Message);
    }

    [Fact]
    public void Create_WithPastTimeSlot_ThrowsBookingException()
    {
        // Arrange
        var userId = UserId.New();
        var pastPeriod = TimeSlot.Create(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1).AddHours(2));
        var vehicle = VehicleInformation.Create("ABC123", VehicleType.Sedan, false, false, true);
        var eventPublisher = new Mock<IEventPublisher>();

        // Act & Assert
        var exception = Assert.Throws<BookingException>(() =>
            BookingRequest.Create(userId, pastPeriod, vehicle, eventPublisher.Object));

        Assert.Equal("Cannot create a booking request for a time period in the past", exception.Message);
    }

    [Fact]
    public void Create_WithSameDayTimeSlot_Succeeds()
    {
        // Arrange
        var userId = UserId.New();
        var sameDayPeriod = TimeSlot.Create(DateTime.UtcNow.Date.AddHours(12), DateTime.UtcNow.Date.AddHours(14)); // Later today
        var vehicle = VehicleInformation.Create("ABC123", VehicleType.Sedan, false, false, true);
        var eventPublisher = new Mock<IEventPublisher>();

        // Act
        var request = BookingRequest.Create(userId, sameDayPeriod, vehicle, eventPublisher.Object);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(BookingRequestStatus.Pending, request.Status);
        Assert.Equal(userId, request.RequestorId);
        Assert.Equal(sameDayPeriod, request.RequestedPeriod);
        Assert.Equal(vehicle, request.Vehicle);
    }

    [Fact]
    public void Create_WithFutureTimeSlot_Succeeds()
    {
        // Arrange
        var userId = UserId.New();
        var futurePeriod = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        var vehicle = VehicleInformation.Create("ABC123", VehicleType.Sedan, false, false, true);
        var eventPublisher = new Mock<IEventPublisher>();

        // Act
        var request = BookingRequest.Create(userId, futurePeriod, vehicle, eventPublisher.Object);

        // Assert
        Assert.NotNull(request);
        Assert.Equal(BookingRequestStatus.Pending, request.Status);
        Assert.Equal(userId, request.RequestorId);
        Assert.Equal(futurePeriod, request.RequestedPeriod);
        Assert.Equal(vehicle, request.Vehicle);
    }

    // Helper method to create a pending request
    private static BookingRequest CreatePendingRequest(out Mock<IEventPublisher> eventPublisher)
    {
        var userId = UserId.New();
        var period = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        var vehicle = VehicleInformation.Create("ABC123", VehicleType.Sedan, false, false, true); // Updated to include IsCompanyCar
        eventPublisher = new Mock<IEventPublisher>();

        return BookingRequest.Create(userId, period, vehicle, eventPublisher.Object);
    }
}