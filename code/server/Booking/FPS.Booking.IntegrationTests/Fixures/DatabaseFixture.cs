// FPS.Booking.IntegrationTests/Fixtures/DatabaseFixture.cs
public class DatabaseFixture : IAsyncLifetime
{
    private readonly TestcontainersContainer _dbContainer;
    public BookingDbContext DbContext { get; private set; }
    
    public DatabaseFixture()
    {
        _dbContainer = new TestcontainersBuilder<PostgreSqlTestcontainer>()
            .WithDatabase(new PostgreSqlTestcontainerConfiguration
            {
                Database = "booking_test_db",
                Username = "test_user",
                Password = "test_password"
            })
            .Build();
    }
    
    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql(_dbContainer.ConnectionString)
            .Options;
            
        DbContext = new BookingDbContext(options);
        await DbContext.Database.MigrateAsync();
    }
    
    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _dbContainer.StopAsync();
    }
}