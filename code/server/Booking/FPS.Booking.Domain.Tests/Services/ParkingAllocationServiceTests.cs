namespace FPS.Booking.Domain.Tests.Services;

public class ParkingAllocationServiceTests
{
    private readonly Mock<ISlotAllocationRepository> _slotAllocationRepositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly ParkingAllocationService _service;

    public ParkingAllocationServiceTests()
    {
        _slotAllocationRepositoryMock = new Mock<ISlotAllocationRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _service = new ParkingAllocationService(_slotAllocationRepositoryMock.Object, _eventPublisherMock.Object);
    }

    [Fact]
    public async Task IsSlotAvailableForPeriodAsync_WhenSlotIsAvailable_ReturnsTrue()
    {
        // Arrange
        var slotId = ParkingSlotId.FromString("A101");
        var period = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        _slotAllocationRepositoryMock
            .Setup(repo => repo.GetBySlotIdForPeriodAsync(slotId, period))
            .ReturnsAsync(new List<SlotAllocation>());

        // Act
        var result = await _service.IsSlotAvailableForPeriodAsync(slotId, period);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsSlotAvailableForPeriodAsync_WhenSlotIsNotAvailable_ReturnsFalse()
    {
        // Arrange
        var slotId = ParkingSlotId.FromString("A101");
        var period = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        var overlappingAllocation = SlotAllocation.CreateAllocation(
            BookingRequestId.New(),
            slotId,
            period,
            _eventPublisherMock.Object);

        _slotAllocationRepositoryMock
            .Setup(repo => repo.GetBySlotIdForPeriodAsync(slotId, period))
            .ReturnsAsync(new List<SlotAllocation> { overlappingAllocation });

        // Act
        var result = await _service.IsSlotAvailableForPeriodAsync(slotId, period);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AllocateSlotAsync_WhenSlotIsAvailable_CreatesAllocation()
    {
        // Arrange
        var request = CreatePendingBookingRequest();
        var slotId = ParkingSlotId.FromString("A101");

        _slotAllocationRepositoryMock
            .Setup(repo => repo.GetBySlotIdForPeriodAsync(slotId, request.RequestedPeriod))
            .ReturnsAsync(new List<SlotAllocation>());

        _slotAllocationRepositoryMock
            .Setup(repo => repo.AddAsync(It.IsAny<SlotAllocation>()))
            .Returns(Task.CompletedTask);

        // Act
        var allocation = await _service.AllocateSlotAsync(request, slotId);

        // Assert
        Assert.NotNull(allocation);
        Assert.Equal(slotId, allocation.SlotId);
        Assert.Equal(request.Id, allocation.BookingRequestId);
        Assert.Equal(SlotAllocationStatus.Reserved, allocation.Status);

        // Verify domain event was published
        _eventPublisherMock.Verify(p => p.PublishAsync(It.Is<SlotAllocationCreatedEvent>(e =>
            e.AllocationId == allocation.Id &&
            e.RequestId == request.Id &&
            e.SlotId == slotId &&
            e.Period == request.RequestedPeriod), default), Times.Once);

        // Verify repository interaction
        _slotAllocationRepositoryMock.Verify(repo => repo.AddAsync(It.IsAny<SlotAllocation>()), Times.Once);
    }

    [Fact]
    public async Task AllocateSlotAsync_WhenSlotIsNotAvailable_ThrowsBookingException()
    {
        // Arrange
        var request = CreatePendingBookingRequest();
        var slotId = ParkingSlotId.FromString("A101");

        _slotAllocationRepositoryMock
            .Setup(repo => repo.GetBySlotIdForPeriodAsync(slotId, request.RequestedPeriod))
            .ReturnsAsync(new List<SlotAllocation>
            {
                SlotAllocation.CreateAllocation(
                    BookingRequestId.New(),
                    slotId,
                    request.RequestedPeriod,
                    _eventPublisherMock.Object)
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            _service.AllocateSlotAsync(request, slotId));

        Assert.Equal($"Slot {slotId} is not available for the requested period", exception.Message);
    }

    [Fact]
    public async Task ValidateAllocationFeasibilityAsync_WhenFeasible_ReturnsTrue()
    {
        // Arrange
        var request = CreatePendingBookingRequest();
        var overlappingAllocation = SlotAllocation.CreateAllocation(
            BookingRequestId.New(),
            ParkingSlotId.FromString("A101"),
            request.RequestedPeriod,
            _eventPublisherMock.Object);

        _slotAllocationRepositoryMock
            .Setup(repo => repo.GetActiveAllocationsAsync())
            .ReturnsAsync(new List<SlotAllocation> { overlappingAllocation });

        // Act
        var result = await _service.ValidateAllocationFeasibilityAsync(request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAllocationFeasibilityAsync_WhenNotFeasible_ReturnsFalse()
    {
        // Arrange
        var request = CreatePendingBookingRequest();

        _slotAllocationRepositoryMock
            .Setup(repo => repo.GetActiveAllocationsAsync())
            .ReturnsAsync(new List<SlotAllocation>());

        // Act
        var result = await _service.ValidateAllocationFeasibilityAsync(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAllocationFeasibilityAsync_WhenRequestIsNotPending_ThrowsBookingException()
    {
        // Arrange
        var request = CreatePendingBookingRequest();
        request.Accept(_eventPublisherMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            _service.ValidateAllocationFeasibilityAsync(request));

        Assert.Equal("Cannot validate feasibility for non-pending requests", exception.Message);
    }

    private BookingRequest CreatePendingBookingRequest()
    {
        var userId = UserId.New();
        var period = TimeSlot.Create(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2));
        var vehicle = VehicleInformation.Create("ABC123", VehicleType.Sedan, false, false, true); // Updated to include IsCompanyCar

        return BookingRequest.Create(userId, period, vehicle, _eventPublisherMock.Object);
    }
}