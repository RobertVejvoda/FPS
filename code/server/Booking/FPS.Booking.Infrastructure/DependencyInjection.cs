using FPS.Booking.Domain.Entities;
using FPS.Booking.Infrastructure.Persistence;
using FPS.Booking.Infrastructure.Persistence.Repositories;
using FPS.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FPS.Booking.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Register DbContext
            services.AddDbContext<BookingDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(BookingDbContext).Assembly.FullName)));

            // Register repositories
            services.AddScoped<IRepository<BookingRequest, BookingRequestId>, BookingRequestRepository>();

            return services;
        }
    }
}