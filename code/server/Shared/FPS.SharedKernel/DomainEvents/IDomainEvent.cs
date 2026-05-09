namespace FPS.SharedKernel.DomainEvents;

/// <summary>
/// Marker interface for all domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the unique identifier for this event instance
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// Gets when this event occurred
    /// </summary>
    DateTimeOffset OccurredOn { get; }
}