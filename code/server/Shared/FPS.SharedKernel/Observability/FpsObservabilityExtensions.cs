using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FPS.SharedKernel.Observability;

public static class FpsObservabilityExtensions
{
    // Registers OpenTelemetry tracing with OTLP export. serviceName should be a
    // short kebab-case identifier (e.g. "fps-identity"). The OTLP endpoint is
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
    // request. Call after UseRouting() and after the OTel SDK is registered so
    // Activity.Current is populated by AspNetCore instrumentation.
    public static IApplicationBuilder UseFpsRequestTraceLogging(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            if (activity is not null)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("FPS.Request");
                logger.LogInformation(
                    "{Method} {Path} TraceId={TraceId} SpanId={SpanId}",
                    context.Request.Method,
                    context.Request.Path,
                    activity.TraceId,
                    activity.SpanId);
            }
            await next(context);
        });
    }
}
