// FPS.Booking.API.Tests/Controllers/BookingsControllerTests.cs

using FPS.Booking.Domain.ValueObjects;

public class BookingsControllerTests
{
    private readonly BookingsController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<DaprClient> _daprClientMock;
    
    public BookingsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _daprClientMock = new Mock<DaprClient>();
        _controller = new BookingsController(_mediatorMock.Object, _daprClientMock.Object);
    }
    
    [Fact]
    public async Task CreateBooking_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateBookingRequestDto(/* parameters */);
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateBookingRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingId.New());
        
        // Act
        var result = await _controller.CreateBooking(request);
        
        // Assert
        var actionResult = Assert.IsType<CreatedAtActionResult>(result);
        actionResult.StatusCode.Should().Be(201);
    }
}