using Microsoft.AspNetCore.Builder;
using Prometheus;
using Prometheus.SystemMetrics;

namespace FPS.SharedKernel.Observability;

public static class FpsMetricsExtensions
{
    // Registers Prometheus metrics collection (system metrics: GC, thread pool, heap).
    public static IServiceCollection AddFpsMetrics(this IServiceCollection services)
    {
        services.AddSystemMetrics();
        return services;
    }

    // Adds HTTP request instrumentation (rate, latency, status codes).
    // Call before UseAuthentication() in the pipeline.
    public static IApplicationBuilder UseFpsMetrics(this IApplicationBuilder app)
    {
        app.UseHttpMetrics(options => options.ReduceStatusCodeCardinality());
        return app;
    }

    // Exposes GET /metrics for Prometheus scraping.
    // Call alongside MapFpsHealthChecks().
    public static WebApplication MapFpsMetrics(this WebApplication app)
    {
        app.MapMetrics();
        return app;
    }
}
