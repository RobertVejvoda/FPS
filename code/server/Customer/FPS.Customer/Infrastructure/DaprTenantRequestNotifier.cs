using Dapr.Client;
using FPS.Customer.Application;
using FPS.Customer.Domain;
using Microsoft.Extensions.Logging;

namespace FPS.Customer.Infrastructure;

/// <summary>
/// PLAT004b — publishes a <see cref="TenantRequestEvent"/> to the Notification pub/sub so the
/// Notification service emails sales. Publish failures are caught and logged: a sales-alert hiccup
/// must never fail the public intake (the request is already durably recorded by the time this runs).
/// Only non-sensitive fields are published; prospect contact PII stays in the durable store.
/// </summary>
public sealed class DaprTenantRequestNotifier(
    DaprClient daprClient, ILogger<DaprTenantRequestNotifier> logger) : ITenantRequestNotifier
{
    private const string PubSubName = "fairspot-pubsub";
    private const string Topic = "tenant-request-received";

    public async Task NotifySalesAsync(TenantRequest request, CancellationToken ct)
    {
        var @event = new TenantRequestEvent(request.RequestId, request.Company, request.PrimaryDomain, request.CreatedAt);
        try
        {
            await daprClient.PublishEventAsync(PubSubName, Topic, @event, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to publish sales alert for tenant request {RequestId}; the request is recorded and visible in the operator queue.",
                request.RequestId);
        }
    }
}
