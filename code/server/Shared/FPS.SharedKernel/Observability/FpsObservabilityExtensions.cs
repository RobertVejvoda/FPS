using System.Diagnostics;
using FPS.SharedKernel.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FPS.SharedKernel.Observability;

public static class FpsObservabilityExtensions
{
    // Registers OpenTelemetry tracing with OTLP export. serviceName should be a
    // short kebab-case identifier (e.g. "fairspot-identity"). The OTLP endpoint is
    // read from Otlp:Endpoint config or the standard OTEL_EXPORTER_OTLP_ENDPOINT
    // env var; defaults to Jaeger's OTLP HTTP traces endpoint for local development.
    public static IServiceCollection AddFpsObservability(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration)
    {
        var endpoint = configuration["Otlp:Endpoint"]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
            ?? "http://localhost:4318/v1/traces";
        endpoint = NormalizeOtlpHttpTraceEndpoint(endpoint);

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                .AddAspNetCoreInstrumentation(opts =>
                {
                    opts.RecordException = true;
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(endpoint);
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                }));

        return services;
    }

    private static string NormalizeOtlpHttpTraceEndpoint(string endpoint)
    {
        var normalized = endpoint.TrimEnd('/');
        return normalized.EndsWith("/v1/traces", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized}/v1/traces";
    }

    // Middleware that logs the active TraceId and SpanId at the start of each
    // request. Call after UseRouting()/UseAuthentication() and after the OTel SDK is
    // registered so Activity.Current is populated and the validated tenant claim is available.
    public static IApplicationBuilder UseFpsRequestTraceLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            if (activity is not null)
            {
                // PLAT005B — tenant observability dimension. The tenant id comes ONLY from the
                // validated claim via ICurrentUser (never a caller-supplied header/body). Platform,
                // health, and unauthenticated requests log the __none__ sentinel and leave the span
                // tag unset. traceId/spanId correlation is unchanged.
                var tenantId = TenantTelemetry.Resolve(context.RequestServices.GetService<ICurrentUser>());
                TenantTelemetry.SetTenantTag(activity, tenantId);

                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("FPS.Request");
                logger.LogInformation(
                    "{Method} {Path} TraceId={TraceId} SpanId={SpanId} tenant_id={TenantId}",
                    context.Request.Method,
                    context.Request.Path,
                    activity.TraceId,
                    activity.SpanId,
                    tenantId);
            }
            await next(context);
        });
    }
}
