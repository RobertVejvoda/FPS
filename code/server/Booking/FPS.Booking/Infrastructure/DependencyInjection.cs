using FPS.Booking.Application.Commands;
using FPS.Booking.Application.Queries;
using FPS.Booking.Application.Repositories;
using FPS.Booking.Application.Services;
using FPS.Booking.Application.Workflows;
using FPS.Booking.Domain.Services;
using FPS.Booking.Infrastructure.Repositories;
using FPS.Booking.Infrastructure.Services;
using FPS.Booking.Infrastructure.Workflows;
using FPS.SharedKernel.DomainEvents;
using FPS.SharedKernel.Profile;
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
        services.AddScoped<ICorrectionAuditRepository, DaprCorrectionAuditRepository>();
        services.AddSingleton<IEmployeeMetricsService, DaprEmployeeMetricsService>();
        // Configuration service is the authoritative slot source (#665); the appsettings-backed
        // ConfiguredAvailableSlotService stays registered as the resilient fallback.
        services.AddScoped<ConfiguredAvailableSlotService>();
        services.AddScoped<IAvailableSlotService, ConfigurationSlotService>();
        // PLAT-seats (#710) — server-side module boundary: resolve a tenant's enabled modules from Customer.
        services.AddScoped<ITenantModulesService, DaprTenantModulesService>();
        services.AddHttpClient<IProfileSnapshotService, HttpProfileSnapshotService>(client =>
            client.BaseAddress = new Uri(configuration["ProfileService:BaseUrl"] ?? "http://fps-profile"));
        services.AddSingleton<DrawService>();
        services.AddScoped<BookingDaprEventPublisher>();
        services.AddScoped<IBookingEventPublisher>(sp => sp.GetRequiredService<BookingDaprEventPublisher>());
        services.AddScoped<IEventPublisher>(sp => sp.GetRequiredService<BookingDaprEventPublisher>());
        services.AddScoped<ITenantPolicyService, DefaultTenantPolicyService>();
        services.AddScoped<IDrawWorkflowStarter, DaprDrawWorkflowStarter>();
        services.AddScoped<IDrawSchedulerService, DrawSchedulerService>();

        return services;
    }
}
