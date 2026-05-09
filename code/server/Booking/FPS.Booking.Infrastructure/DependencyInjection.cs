using FPS.Booking.Infrastructure.Repositories;
using FPS.Booking.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FPS.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<DaprBookingRepository>();
        services.AddScoped<DaprAllocationService>();
        services.AddScoped<DaprEventPublisher>();
        return services;
    }
}
