namespace FPS.SharedKernel.Domain;

public abstract class StronglyTypedId<T> : ValueObject where T : notnull
{
    public T Value { get; }

    protected StronglyTypedId(T value)
    {
        Value = value;
    }

    // Fix: Handle potential null return from ToString()
    public override string ToString() => Value.ToString() ?? string.Empty;

    // Fix: Add nullable annotation to match base class signature
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
            
        if (obj.GetType() != GetType())
            return false;
            
        return Equals((StronglyTypedId<T>)obj);
    }

    protected bool Equals(StronglyTypedId<T> other)
    {
        return EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static bool operator ==(StronglyTypedId<T>? left, StronglyTypedId<T>? right)
    {
        if (left is null && right is null)
            return true;
            
        if (left is null || right is null)
            return false;
            
        return left.Equals(right);
    }

    public static bool operator !=(StronglyTypedId<T>? left, StronglyTypedId<T>? right) => !(left == right);
}