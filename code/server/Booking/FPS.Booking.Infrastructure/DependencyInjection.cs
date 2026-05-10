using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Queries;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Domain.Services;
using FPS.Booking.Infrastructure.Repositories;
using FPS.Booking.Infrastructure.Services;
using FPS.SharedKernel.DomainEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FPS.Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SubmitBookingRequestHandler).Assembly));

        services.AddScoped<IBookingRepository, DaprBookingRepository>();
        services.AddScoped<IBookingQueryRepository, DaprBookingQueryRepository>();
        services.AddScoped<IDrawRepository, DaprDrawRepository>();
        services.AddScoped<IPenaltyRepository, DaprPenaltyRepository>();
        services.AddSingleton<IEmployeeMetricsService, InMemoryEmployeeMetricsService>();
        services.AddScoped<IAvailableSlotService, ConfiguredAvailableSlotService>();
        services.AddSingleton<DrawService>();
        services.AddScoped<IAllocationService, DaprAllocationService>();
        services.AddScoped<IEventPublisher, DaprEventPublisher>();
        services.AddScoped<ITenantPolicyService, DefaultTenantPolicyService>();

        return services;
    }
}
