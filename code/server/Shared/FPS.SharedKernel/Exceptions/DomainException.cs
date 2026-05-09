namespace FPS.SharedKernel.Exceptions;

/// <summary>
/// A base exception class for domain-related errors.
/// </summary>
public abstract class DomainException : BaseException
{
    protected DomainException(string message) : base(message) { }

    protected DomainException(string message, Exception innerException) : base(message, innerException) { }

}