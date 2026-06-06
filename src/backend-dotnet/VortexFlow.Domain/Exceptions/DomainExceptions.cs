namespace VortexFlow.Domain.Exceptions;

/// <summary>
/// Base exception for domain-level expected failures that should map to specific HTTP responses.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string resource, object key)
        : base($"{resource} with key '{key}' was not found.") { }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string reason)
        : base($"Access denied: {reason}") { }
}

public sealed class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
