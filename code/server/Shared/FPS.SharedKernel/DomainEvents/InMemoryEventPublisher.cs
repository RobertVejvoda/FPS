using System.Collections.Concurrent;

namespace FPS.SharedKernel.DomainEvents;

public class InMemoryEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<IDomainEvent> _events = new();

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        _events.Enqueue(domainEvent);
        return Task.CompletedTask;
    }

    public IEnumerable<IDomainEvent> GetPublishedEvents() => _events.ToArray();
}