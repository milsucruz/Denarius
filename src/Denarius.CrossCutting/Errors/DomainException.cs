namespace Denarius.CrossCutting.Errors;

/// <summary>
/// Thrown when an aggregate invariant is violated at runtime.
/// Represents a programming error or an illegal state transition, not an expected business failure.
/// Expected business failures should be expressed as <see cref="Results.Result.Failure(DomainError)"/> instead.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainError Error { get; }

    public DomainException(DomainError error)
        : base(error.Message)
    {
        Error = error;
    }
}
