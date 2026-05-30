using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FPS.DataHub.Infrastructure;

// Supports `dotnet ef migrations add` and `dotnet ef database update` without a running host.
public sealed class DataHubDbContextFactory : IDesignTimeDbContextFactory<DataHubDbContext>
{
    public DataHubDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DataHubDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=fps_datahub;Username=fps;Password=fps",
                npgsql => npgsql.MigrationsAssembly(typeof(DataHubDbContext).Assembly.FullName))
            .Options;

        return new DataHubDbContext(options);
    }
}
