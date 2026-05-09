public class BookingRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly BookingRepository _repository;
    private readonly BookingDbContext _dbContext;
    
    public BookingRepositoryTests(DatabaseFixture fixture)
    {
        _dbContext = fixture.DbContext;
        _repository = new BookingRepository(_dbContext);
    }
    
    [Fact]
    public async Task GetByIdAsync_ExistingBooking_ReturnsBooking()
    {
        // Arrange
        var booking = CreateTestBooking();
        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();
        
        // Act
        var result = await _repository.GetByIdAsync(booking.Id);
        
        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(booking.Id);
    }
}