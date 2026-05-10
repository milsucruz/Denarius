using Denarius.CrossCutting.Errors;

namespace Denarius.CrossCutting.Results;

/// <summary>
/// Discriminated union representing either a successful operation or a typed domain failure.
/// Use <see cref="Success()"/> and <see cref="Failure(DomainError)"/> to construct instances.
/// </summary>
public class Result
{
    // ── Properties ─────────────────────────────────────────────────────────────

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DomainError Error { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    protected Result(bool isSuccess, DomainError error)
    {
        if (isSuccess && error != DomainError.None)
        {
            throw new ArgumentException("A successful result must not carry an error.", nameof(error));
        }

        if (!isSuccess && error == DomainError.None)
        {
            throw new ArgumentException("A failed result must carry a non-None error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    // ── Static factories ───────────────────────────────────────────────────────

    /// <summary>Returns a successful result with no value.</summary>
    public static Result Success()
    {
        return new Result(true, DomainError.None);
    }

    /// <summary>Returns a failed result carrying the specified domain error.</summary>
    public static Result Failure(DomainError error)
    {
        return new Result(false, error);
    }

    /// <summary>Returns a successful result carrying a value.</summary>
    public static Result<TValue> Success<TValue>(TValue value)
    {
        return Result<TValue>.Create(value);
    }

    /// <summary>Returns a failed result of type <typeparamref name="TValue"/> carrying the specified domain error.</summary>
    public static Result<TValue> Failure<TValue>(DomainError error)
    {
        return Result<TValue>.Failure(error);
    }

    // ── Implicit conversions ───────────────────────────────────────────────────

    /// <summary>
    /// Allows returning a <see cref="DomainError"/> directly from a method whose return type is <see cref="Result"/>.
    /// Equivalent to calling <see cref="Failure(DomainError)"/>.
    /// </summary>
    public static implicit operator Result(DomainError error)
    {
        return Failure(error);
    }
}
