using Denarius.CrossCutting.Errors;

namespace Denarius.CrossCutting.Results;

/// <summary>
/// Discriminated union representing either a successful operation with a value
/// or a typed domain failure. Obtain instances via <see cref="Result.Success{TValue}(TValue)"/>
/// and <see cref="Result.Failure{TValue}(DomainError)"/>.
/// </summary>
public sealed class Result<TValue> : Result
{
    // ── Properties ─────────────────────────────────────────────────────────────

    public TValue? Value { get; }

    // ── Constructor ────────────────────────────────────────────────────────────

    private Result(TValue? value, bool isSuccess, DomainError error)
        : base(isSuccess, error)
    {
        Value = value;
    }

    // ── Internal factories (called from Result static methods) ─────────────────

    internal static Result<TValue> Create(TValue value)
    {
        return new Result<TValue>(value, true, DomainError.None);
    }

    internal static new Result<TValue> Failure(DomainError error)
    {
        return new Result<TValue>(default, false, error);
    }

    // ── Implicit conversion ────────────────────────────────────────────────────

    /// <summary>
    /// Allows a value to be returned directly as a successful <see cref="Result{TValue}"/>
    /// without explicitly calling <see cref="Result.Success{TValue}(TValue)"/>.
    /// </summary>
    public static implicit operator Result<TValue>(TValue value)
    {
        return Create(value);
    }
}
