namespace FPS.SharedKernel.Interfaces;

/// <summary>
/// Marker interface for domain events
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// When the domain event occurred
    /// </summary>
    DateTime OccurredOn { get; }
    
    /// <summary>
    /// Unique identifier for the event
    /// </summary>
    Guid EventId { get; }
}