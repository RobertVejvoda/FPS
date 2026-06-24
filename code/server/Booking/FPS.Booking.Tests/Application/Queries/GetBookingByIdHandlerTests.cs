namespace FPS.Booking.Application.Tests.Queries;

public sealed class GetBookingByIdHandlerTests
{
    private readonly Mock<IBookingRepository> repository = new();
    private readonly GetBookingByIdHandler handler;

    public GetBookingByIdHandlerTests()
    {
        handler = new GetBookingByIdHandler(repository.Object);
    }

    [Fact]
    public async Task Handle_BookingExists_AndOwnerMatches_ReturnsResult()
    {
        var id = Guid.NewGuid();
        repository
            .Setup(r => r.GetBookingRequestAsync("t-1", id))
            .ReturnsAsync(SampleDto(id, requestedBy: "user-1"));

        var result = await handler.Handle(new GetBookingByIdQuery("t-1", "user-1", id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(id, result.RequestId);
        Assert.Equal("Allocated", result.Status);
    }

    [Fact]
    public async Task Handle_BookingDoesNotExist_ReturnsNull()
    {
        repository
            .Setup(r => r.GetBookingRequestAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync((BookingRequestDto?)null);

        var result = await handler.Handle(
            new GetBookingByIdQuery("t-1", "user-1", Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_BookingBelongsToDifferentUser_ReturnsNull()
    {
        var id = Guid.NewGuid();
        repository
            .Setup(r => r.GetBookingRequestAsync("t-1", id))
            .ReturnsAsync(SampleDto(id, requestedBy: "user-other"));

        var result = await handler.Handle(new GetBookingByIdQuery("t-1", "user-1", id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_MapsAllFieldsFromDto()
    {
        var id = Guid.NewGuid();
        var arrival = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);
        var departure = new DateTime(2026, 7, 1, 17, 0, 0, DateTimeKind.Utc);
        var dto = new BookingRequestDto
        {
            RequestId = id,
            TenantId = "t-1",
            RequestedBy = "user-1",
            Status = "Pending",
            LocationId = "loc-1",
            PlannedArrivalTime = arrival,
            PlannedDepartureTime = departure,
            AllocatedSlotId = null,
            VehicleType = "Sedan",
            VehicleIsElectric = true,
            RequiresAccessibleSpot = false,
            VehicleIsCompanyCar = false,
            RequestedAt = arrival.AddDays(-1),
            LastStatusChangedAt = arrival.AddDays(-1),
        };
        repository.Setup(r => r.GetBookingRequestAsync("t-1", id)).ReturnsAsync(dto);

        var result = await handler.Handle(new GetBookingByIdQuery("t-1", "user-1", id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("loc-1", result.LocationId);
        Assert.Equal(arrival, result.PlannedArrivalTime);
        Assert.Equal("Sedan", result.VehicleType);
        Assert.True(result.VehicleIsElectric);
    }

    private static BookingRequestDto SampleDto(Guid id, string requestedBy) => new()
    {
        RequestId = id,
        TenantId = "t-1",
        RequestedBy = requestedBy,
        Status = "Allocated",
        LocationId = "loc-1",
        PlannedArrivalTime = DateTime.UtcNow.Date.AddHours(9),
        PlannedDepartureTime = DateTime.UtcNow.Date.AddHours(17),
        AllocatedSlotId = "P1-5",
        VehicleType = "Sedan",
    };
}
