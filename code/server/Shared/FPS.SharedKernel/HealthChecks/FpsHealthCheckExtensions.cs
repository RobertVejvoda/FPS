using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace FPS.SharedKernel.HealthChecks;

public static class FpsHealthCheckExtensions
{
    // Registers health check services. Call in each service Program.cs.
    // Optionally chain .AddDapr() for services with Dapr sidecars.
    public static IHealthChecksBuilder AddFpsHealthChecks(this IServiceCollection services)
        => services.AddHealthChecks();

    // Maps GET /health returning JSON with status and component details.
    public static WebApplication MapFpsHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (ctx, report) =>
            {
                ctx.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    duration = report.TotalDuration,
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        duration = e.Value.Duration,
                        description = e.Value.Description,
                    }),
                });
                await ctx.Response.WriteAsync(result);
            },
        });
        return app;
    }
}
