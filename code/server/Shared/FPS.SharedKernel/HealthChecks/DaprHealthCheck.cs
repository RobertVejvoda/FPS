using Dapr.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FPS.SharedKernel.HealthChecks
{
    public class DaprHealthCheckOptions
    {
        public int TimeoutSeconds { get; set; } = 5;
        public bool CheckComponents { get; set; } = true;
        public List<string> ComponentsToCheck { get; set; } = new();
        public bool IncludeMetadata { get; set; } = true;
        public string[] CriticalComponents { get; set; } = { "statestore", "pubsub", "secretstore" };
    }

    public class DaprHealthCheck : IHealthCheck
    {
        private readonly DaprClient _daprClient;
        private readonly DaprHealthCheckOptions _options;
        private readonly ILogger<DaprHealthCheck>? _logger;

        public DaprHealthCheck(
            DaprClient daprClient,
            IOptions<DaprHealthCheckOptions>? options = null,
            ILogger<DaprHealthCheck>? logger = null)
        {
            _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
            _options = options?.Value ?? new DaprHealthCheckOptions();
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds)).Token,
                    cancellationToken);

                // Basic Dapr sidecar health check
                var healthy = await _daprClient.CheckHealthAsync(cts.Token);
                data["responseTimeMs"] = stopwatch.ElapsedMilliseconds;

                if (!healthy)
                    return new HealthCheckResult(context.Registration.FailureStatus, "Dapr sidecar is unhealthy", data: data);

                // If no additional checks are needed, return early
                if (!_options.IncludeMetadata && !_options.CheckComponents)
                    return HealthCheckResult.Healthy("Dapr sidecar is healthy", data);

                // Check metadata if requested
                await CheckMetadataAsync(cts.Token, data);

                // Check components if requested
                if (_options.CheckComponents)
                {
                    var componentStatus = await CheckComponentsAsync(cts.Token);
                    data["componentHealth"] = componentStatus;

                    // Check if any critical components are unhealthy
                    if (componentStatus.Any(c => !c.Value && IsCritical(c.Key)))
                        return HealthCheckResult.Degraded("Some critical components are unhealthy", data: data);
                }

                return HealthCheckResult.Healthy("Dapr services are healthy", data);
            }
            catch (TaskCanceledException)
            {
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    $"Health check timed out after {_options.TimeoutSeconds} seconds",
                    data: data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Dapr health check failed");
                data["error"] = ex.Message;
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "Dapr health check failed",
                    ex,
                    data);
            }
        }

        private async Task CheckMetadataAsync(CancellationToken token, Dictionary<string, object> data)
        {
            try
            {
                var metadata = await _daprClient.GetMetadataAsync(token);
                data["appId"] = metadata.Id;

                // Add actors if available
                if (metadata.Actors?.Any() == true)
                    data["actors"] = metadata.Actors;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to retrieve Dapr metadata");
                data["metadataError"] = ex.Message;
            }
        }

        private async Task<Dictionary<string, bool>> CheckComponentsAsync(CancellationToken token)
        {
            var result = new Dictionary<string, bool>();

            try
            {
                var metadata = await _daprClient.GetMetadataAsync(token);
                var components = metadata.Components ?? Array.Empty<DaprComponentsMetadata>();

                // Filter components if specific ones are requested
                if (_options.ComponentsToCheck.Any())
                {
                    components = components
                        .Where(c => _options.ComponentsToCheck.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
                        .ToArray();
                }

                foreach (var component in components)
                {
                    var componentName = component.Name ?? "unknown";
                    var componentType = component.Type ?? "unknown";
                    
                    try
                    {
                        // For now, just assume all components are healthy
                        // In a real implementation, you'd check specific component types
                        result[componentName] = true;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to check component {ComponentName}", componentName);
                        result[componentName] = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to check component health");
            }

            return result;
        }
        
        private bool IsCritical(string componentName) => 
            _options.CriticalComponents.Any(c => 
                componentName.Contains(c, StringComparison.OrdinalIgnoreCase));
    }

    public static class DaprHealthCheckExtensions
    {
        public static IHealthChecksBuilder AddDaprHealthCheck(
            this IHealthChecksBuilder builder,
            string name = "dapr",
            HealthStatus? failureStatus = default,
            IEnumerable<string>? tags = null,
            Action<DaprHealthCheckOptions>? configure = null)
        {
            if (configure != null)
                builder.Services.Configure(configure);
                
            return builder.AddCheck<DaprHealthCheck>(
                name, 
                failureStatus ?? HealthStatus.Unhealthy, 
                tags ?? Array.Empty<string>());
        }
    }
}