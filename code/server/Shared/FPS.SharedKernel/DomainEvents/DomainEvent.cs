namespace FPS.SharedKernel.DomainEvents;

/// <summary>
/// Base class for all domain events
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <summary>
    /// Unique identifier for the event
    /// </summary>
    public Guid EventId { get; }
    
    /// <summary>
    /// When the domain event occurred
    /// </summary>
    public DateTimeOffset OccurredOn { get; }

    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }
}