using Dapr.Client;
using FPS.SharedKernel.DomainEvents;

namespace FPS.Booking.Infrastructure.Services;

public class DaprEventPublisher : IEventPublisher
{
    private readonly DaprClient _daprClient;
    private const string PubsubName = "rabbitmq-pubsub";

    public DaprEventPublisher(DaprClient daprClient) => _daprClient = daprClient;

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var topic = typeof(TEvent).Name;
        await _daprClient.PublishEventAsync(PubsubName, topic, domainEvent, cancellationToken);
    }
}
