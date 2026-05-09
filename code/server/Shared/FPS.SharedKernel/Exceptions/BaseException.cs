namespace FPS.SharedKernel.Exceptions;

/// <summary>
/// A base exception class for all custom exceptions in the system.
/// </summary>
public abstract class BaseException : Exception
{
    /// <summary>
    /// Additional metadata or context for the exception.
    /// </summary>
    public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

    protected BaseException(string message) : base(message) { }

    protected BaseException(string message, Exception innerException) : base(message, innerException) { }

    /// <summary>
    /// Adds metadata to the exception for additional context.
    /// </summary>
    public BaseException WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }
}