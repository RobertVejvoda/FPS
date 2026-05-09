namespace FPS.SharedKernel.Interfaces;

/// <summary>
/// A base interface for all entities in the system.
/// </summary>
public interface IEntity<TId>
{
    /// <summary>
    /// The unique identifier for the entity.
    /// </summary>
    TId Id { get; }
}