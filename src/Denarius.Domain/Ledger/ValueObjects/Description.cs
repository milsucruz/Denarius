using Denarius.CrossCutting.Errors;
using Denarius.CrossCutting.Results;

namespace Denarius.Domain.Ledger.ValueObjects;

/// <summary>
/// A non-empty textual description for a ledger operation.
/// </summary>
public sealed record Description
{
    public string Value { get; }

    private Description(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a Description. Fails when <paramref name="value"/> is null, empty, or whitespace.
    /// </summary>
    public static Result<Description> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<Description>(
                DomainError.Create(DomainErrorCode.Validation, "Description cannot be empty."));
        }

        return Result.Success<Description>(new Description(value));
    }

    public static implicit operator string(Description d) => d.Value;
}
