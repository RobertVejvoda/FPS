namespace FPS.SharedKernel.ValueObjects;

/// <summary>
/// Base class for implementing value objects in domain-driven design.
/// Value objects are equality-comparable by their properties rather than by reference.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// When overridden in a derived class, returns all components of the value object that should be used for equality.
    /// </summary>
    /// <returns>An enumerable of objects representing the value object's equality components.</returns>
    protected abstract IEnumerable<object> GetEqualityComponents();

    /// <summary>
    /// Determines whether this value object is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>true if the objects are equal; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current value object.</returns>
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x != null ? x.GetHashCode() : 0)
            .Aggregate((x, y) => x ^ y);
    }

    /// <summary>
    /// Compares two value objects for equality.
    /// </summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns>true if the value objects are equal; otherwise, false.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (ReferenceEquals(left, null) && ReferenceEquals(right, null))
            return true;

        if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// Compares two value objects for inequality.
    /// </summary>
    /// <param name="left">The first value object.</param>
    /// <param name="right">The second value object.</param>
    /// <returns>true if the value objects are not equal; otherwise, false.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Creates a shallow copy of the current value object.
    /// </summary>
    /// <returns>A shallow copy of the current value object.</returns>
    public ValueObject Clone()
    {
        return (ValueObject)MemberwiseClone();
    }
}